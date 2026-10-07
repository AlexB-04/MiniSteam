using Microsoft.Extensions.Options;
using MiniSteam.Configuration;

namespace MiniSteam.Services;

public sealed class SandboxPaymentProvider : ISandboxPaymentProvider
{
    private readonly CommerceOptions _options;

    public SandboxPaymentProvider(IOptions<CommerceOptions> options)
    {
        _options = options.Value;
    }

    public string ProviderName => string.IsNullOrWhiteSpace(_options.SandboxProviderName)
        ? "MiniSteam Sandbox"
        : _options.SandboxProviderName.Trim();

    public string CreateProviderReference() =>
        $"sandbox_{Guid.NewGuid():N}";

    public string CreateRefundReference() =>
        $"sandbox_refund_{Guid.NewGuid():N}";

    public Task<bool> ConfirmAsync(
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(providerReference))
        {
            return Task.FromResult(false);
        }

        // v3.5.x is deliberately sandbox-only. No card data is collected or stored.
        return Task.FromResult(true);
    }
}
