using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class SessionService
{
    public string? Email { get; private set; }
    public string? AccessToken { get; private set; }
    public DateTime AccessTokenExpiresAt { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime RefreshTokenExpiresAt { get; private set; }
    public IReadOnlyList<string> Roles { get; private set; } = Array.Empty<string>();

    public bool HasRefreshToken =>
        !string.IsNullOrWhiteSpace(RefreshToken) &&
        RefreshTokenExpiresAt > DateTime.UtcNow;

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        HasRefreshToken;

    public bool AccessTokenNeedsRefresh =>
        HasRefreshToken &&
        (string.IsNullOrWhiteSpace(AccessToken) ||
         AccessTokenExpiresAt <= DateTime.UtcNow.AddMinutes(1));

    public void Start(string email, TokenResponse response)
    {
        Email = email;
        Apply(response);
    }

    public void Restore(string email, string refreshToken, DateTime refreshTokenExpiresAt)
    {
        Email = email;
        AccessToken = null;
        AccessTokenExpiresAt = default;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAt = refreshTokenExpiresAt;
        Roles = Array.Empty<string>();
    }

    public void Apply(TokenResponse response)
    {
        AccessToken = response.Token;
        AccessTokenExpiresAt = response.ExpiresAt;
        RefreshToken = response.RefreshToken;
        RefreshTokenExpiresAt = response.RefreshTokenExpiresAt;
        Roles = response.Roles;
    }

    public void Clear()
    {
        Email = null;
        AccessToken = null;
        AccessTokenExpiresAt = default;
        RefreshToken = null;
        RefreshTokenExpiresAt = default;
        Roles = Array.Empty<string>();
    }
}
