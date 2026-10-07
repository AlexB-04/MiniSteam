namespace MiniSteam.Configuration;

public sealed class CommerceOptions
{
    public const string SectionName = "Commerce";

    public string Currency { get; set; } = "EUR";

    // v3.5 keeps tax calculation configurable but disabled by default.
    // A real storefront needs region/customer-aware tax logic before enabling it.
    public decimal SandboxTaxRate { get; set; } = 0m;

    public string SandboxProviderName { get; set; } = "MiniSteam Sandbox";
}
