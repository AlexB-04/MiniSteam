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

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(AccessToken) &&
        !string.IsNullOrWhiteSpace(RefreshToken);

    public bool AccessTokenNeedsRefresh =>
        IsAuthenticated && AccessTokenExpiresAt <= DateTime.UtcNow.AddMinutes(1);

    public void Start(string email, TokenResponse response)
    {
        Email = email;
        Apply(response);
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
