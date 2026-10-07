using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class AuthService
{
    private readonly ApiClient _apiClient;
    private readonly SessionService _session;
    private readonly SecureSessionStore _sessionStore;

    public AuthService(ApiClient apiClient, SessionService session, SecureSessionStore sessionStore)
    {
        _apiClient = apiClient;
        _session = session;
        _sessionStore = sessionStore;
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
        _sessionStore.Save(_session);
    }

    public async Task<bool> TryRestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        var saved = _sessionStore.TryLoad();
        if (saved == null)
        {
            return false;
        }

        _session.Restore(saved.Email, saved.RefreshToken, saved.RefreshTokenExpiresAt);

        try
        {
            if (await _apiClient.EnsureValidAccessTokenAsync(cancellationToken))
            {
                return true;
            }
        }
        catch (ApiException)
        {
            // Keep the encrypted refresh token for a later launch if the API is temporarily unavailable.
        }

        _session.Clear();
        return false;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_session.HasRefreshToken)
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
            _sessionStore.Clear();
        }
    }
}
