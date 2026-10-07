namespace MiniSteam.Services;

public interface ISandboxPaymentProvider
{
    string ProviderName { get; }
    string CreateProviderReference();
    string CreateRefundReference();
    Task<bool> ConfirmAsync(string providerReference, CancellationToken cancellationToken = default);
}
