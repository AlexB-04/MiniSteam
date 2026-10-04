using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniSteam;
using MiniSteam.Data;

namespace MiniSteam.Tests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"MiniSteamIntegrationTests-{Guid.NewGuid()}";

    private readonly Dictionary<string, string?> _previousEnvironmentValues = new();

    public TestWebApplicationFactory()
    {
        SetTestEnvironment("ASPNETCORE_ENVIRONMENT", "Testing");
        SetTestEnvironment("Jwt__Issuer", "MiniSteam.Tests");
        SetTestEnvironment("Jwt__Audience", "MiniSteam.Tests.Api");
        SetTestEnvironment("Jwt__Key", "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=");
        SetTestEnvironment("Jwt__AccessTokenMinutes", "15");
        SetTestEnvironment("Jwt__RefreshTokenDays", "7");
        SetTestEnvironment("Database__ApplyMigrationsOnStartup", "false");
        SetTestEnvironment("RateLimit__AuthPermitLimit", "100");
        SetTestEnvironment("RateLimit__AuthWindowSeconds", "60");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=(localdb)\\mssqllocaldb;Database=MiniSteamIntegrationTests;Trusted_Connection=True;",
                ["Jwt:Issuer"] = "MiniSteam.Tests",
                ["Jwt:Audience"] = "MiniSteam.Tests.Api",
                ["Jwt:Key"] = "AQIDBAUGBwgJCgsMDQ4PEBESExQVFhcYGRobHB0eHyA=",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["RateLimit:AuthPermitLimit"] = "100",
                ["RateLimit:AuthWindowSeconds"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DataContext>>();
            services.RemoveAll<DataContext>();

            var providerConfigurationDescriptors = services
                .Where(descriptor =>
                    descriptor.ServiceType.IsGenericType &&
                    descriptor.ServiceType.Name.StartsWith(
                        "IDbContextOptionsConfiguration",
                        StringComparison.Ordinal) &&
                    descriptor.ServiceType.GenericTypeArguments.Contains(typeof(DataContext)))
                .ToList();

            foreach (var descriptor in providerConfigurationDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<DataContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }

    private void SetTestEnvironment(string key, string value)
    {
        _previousEnvironmentValues[key] = Environment.GetEnvironmentVariable(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        foreach (var item in _previousEnvironmentValues)
        {
            Environment.SetEnvironmentVariable(item.Key, item.Value);
        }
    }
}
