using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MiniSteam.Desktop.Configuration;
using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly SessionService _session;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(DesktopSettings settings, SessionService session)
    {
        _session = session;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(settings.Api.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public Uri BaseAddress => _httpClient.BaseAddress!;

    public async Task<T> GetAsync<T>(
        string relativeUrl,
        bool authenticated = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, relativeUrl),
            authenticated,
            cancellationToken);

        return await ReadSuccessAsync<T>(response, cancellationToken);
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string relativeUrl,
        TRequest body,
        bool authenticated = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => CreateJsonRequest(HttpMethod.Post, relativeUrl, body),
            authenticated,
            cancellationToken);

        return await ReadSuccessAsync<TResponse>(response, cancellationToken);
    }

    public async Task PostAsync<TRequest>(
        string relativeUrl,
        TRequest body,
        bool authenticated = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => CreateJsonRequest(HttpMethod.Post, relativeUrl, body),
            authenticated,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }
    }

    public async Task<TResponse> PostAsync<TResponse>(
        string relativeUrl,
        bool authenticated = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, relativeUrl),
            authenticated,
            cancellationToken);

        return await ReadSuccessAsync<TResponse>(response, cancellationToken);
    }

    public async Task<TResponse> PutAsync<TRequest, TResponse>(
        string relativeUrl,
        TRequest body,
        bool authenticated = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => CreateJsonRequest(HttpMethod.Put, relativeUrl, body),
            authenticated,
            cancellationToken);

        return await ReadSuccessAsync<TResponse>(response, cancellationToken);
    }

    public async Task DeleteAsync(
        string relativeUrl,
        bool authenticated = false,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, relativeUrl),
            authenticated,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }
    }

    public string? ResolveAssetUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        return new Uri(BaseAddress, value.TrimStart('/')).ToString();
    }

    public async Task<bool> EnsureValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!_session.IsAuthenticated)
        {
            return false;
        }

        if (!_session.AccessTokenNeedsRefresh)
        {
            return true;
        }

        return await TryRefreshAsync(cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        bool authenticated,
        CancellationToken cancellationToken)
    {
        try
        {
            if (authenticated && !await EnsureValidAccessTokenAsync(cancellationToken))
            {
                throw new ApiException("Your MiniSteam session has expired. Please sign in again.", 401, "SessionExpired");
            }

            using var firstRequest = requestFactory();
            AddAuthorization(firstRequest, authenticated);

            var response = await _httpClient.SendAsync(firstRequest, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized || !authenticated)
            {
                return response;
            }

            response.Dispose();

            if (!await TryRefreshAsync(cancellationToken))
            {
                throw new ApiException("Your MiniSteam session has expired. Please sign in again.", 401, "SessionExpired");
            }

            using var retryRequest = requestFactory();
            AddAuthorization(retryRequest, authenticated: true);
            return await _httpClient.SendAsync(retryRequest, cancellationToken);
        }
        catch (ApiException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(
                $"MiniSteam API is unavailable at {BaseAddress}. Start the web/backend project and try again.",
                innerException: ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiException("MiniSteam API did not respond in time.", innerException: ex);
        }
    }

    private void AddAuthorization(HttpRequestMessage request, bool authenticated)
    {
        if (authenticated && !string.IsNullOrWhiteSpace(_session.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);
        }
    }

    private HttpRequestMessage CreateJsonRequest<TRequest>(HttpMethod method, string relativeUrl, TRequest body)
    {
        var json = JsonSerializer.Serialize(body, _jsonOptions);

        return new HttpRequestMessage(method, relativeUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        var refreshTokenBeforeLock = _session.RefreshToken;

        if (string.IsNullOrWhiteSpace(refreshTokenBeforeLock) ||
            _session.RefreshTokenExpiresAt <= DateTime.UtcNow)
        {
            _session.Clear();
            return false;
        }

        await _refreshLock.WaitAsync(cancellationToken);

        try
        {
            if (_session.RefreshToken != refreshTokenBeforeLock && _session.IsAuthenticated)
            {
                return true;
            }

            using var response = await _httpClient.PostAsJsonAsync(
                "api/auth/refresh",
                new RefreshTokenRequest { RefreshToken = refreshTokenBeforeLock },
                _jsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is HttpStatusCode.BadRequest or
                    HttpStatusCode.Unauthorized or
                    HttpStatusCode.Forbidden)
                {
                    _session.Clear();
                    return false;
                }

                throw await CreateApiExceptionAsync(response, cancellationToken);
            }

            var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>(
                _jsonOptions,
                cancellationToken);

            if (tokenResponse == null ||
                string.IsNullOrWhiteSpace(tokenResponse.Token) ||
                string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            {
                _session.Clear();
                return false;
            }

            _session.Apply(tokenResponse);
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);

        if (result == null)
        {
            throw new ApiException("MiniSteam API returned an empty response.", (int)response.StatusCode);
        }

        return result;
    }

    private async Task<ApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ApiProblem? problem = null;

        try
        {
            problem = await response.Content.ReadFromJsonAsync<ApiProblem>(_jsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            // A non-ProblemDetails response is still surfaced with the HTTP status below.
        }

        var message = problem?.Detail
            ?? problem?.Title
            ?? $"MiniSteam API returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).";

        return new ApiException(
            message,
            (int)response.StatusCode,
            problem?.Code,
            problem?.TraceId);
    }
}
