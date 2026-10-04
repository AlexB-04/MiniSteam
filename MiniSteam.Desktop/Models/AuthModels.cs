namespace MiniSteam.Desktop.Models;

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class RevokeTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public List<string> Roles { get; set; } = new();
}
