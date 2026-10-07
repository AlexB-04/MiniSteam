using MiniSteam.Desktop.Models;

namespace MiniSteam.Desktop.Services;

public sealed class PaymentService
{
    private readonly ApiClient _apiClient;

    public PaymentService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<PaymentDto> CreateCheckoutAsync(CancellationToken cancellationToken = default)
    {
        return _apiClient.PostAsync<PaymentDto>(
            "api/payments/checkout",
            authenticated: true,
            cancellationToken: cancellationToken);
    }

    public Task<PaymentDto> GetAsync(
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.GetAsync<PaymentDto>(
            $"api/payments/{paymentId}",
            authenticated: true,
            cancellationToken: cancellationToken);
    }

    public async Task<PaymentDto?> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _apiClient.GetAsync<PaymentDto>(
                "api/payments/pending",
                authenticated: true,
                cancellationToken: cancellationToken);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public Task<PaymentDto> ConfirmSandboxAsync(
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.PostAsync<PaymentDto>(
            $"api/payments/{paymentId}/sandbox/confirm",
            authenticated: true,
            cancellationToken: cancellationToken);
    }

    public Task<PaymentDto> FailSandboxAsync(
        int paymentId,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.PostAsync<PaymentDto>(
            $"api/payments/{paymentId}/sandbox/fail",
            authenticated: true,
            cancellationToken: cancellationToken);
    }

    public Task<PaymentDto> RefundSandboxAsync(
        int paymentId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.PostAsync<RefundPaymentRequest, PaymentDto>(
            $"api/payments/{paymentId}/sandbox/refund",
            new RefundPaymentRequest { Reason = reason },
            authenticated: true,
            cancellationToken: cancellationToken);
    }

    public Task<List<PaymentDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return _apiClient.GetAsync<List<PaymentDto>>(
            "api/payments",
            authenticated: true,
            cancellationToken: cancellationToken);
    }
}
