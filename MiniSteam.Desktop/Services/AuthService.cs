using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class AuthService
{
    private readonly ApiClient _apiClient;
    private readonly SessionService _session;

    public AuthService(ApiClient apiClient, SessionService session)
    {
        _apiClient = apiClient;
        _session = session;
    }

    public async Task LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();

        var response = await _apiClient.PostAsync<LoginRequest, TokenResponse>(
            "api/auth/login",
            new LoginRequest
            {
                Email = normalizedEmail,
                Password = password
            },
            authenticated: false,
            cancellationToken: cancellationToken);

        _session.Start(normalizedEmail, response);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_session.IsAuthenticated)
            {
                return;
            }

            if (!await _apiClient.EnsureValidAccessTokenAsync(cancellationToken))
            {
                return;
            }

            var refreshToken = _session.RefreshToken;

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                await _apiClient.PostAsync(
                    "api/auth/revoke",
                    new RevokeTokenRequest { RefreshToken = refreshToken },
                    authenticated: true,
                    cancellationToken: cancellationToken);
            }
        }
        catch (ApiException)
        {
            // Local logout must still succeed if the server session is already gone.
        }
        finally
        {
            _session.Clear();
        }
    }
}
