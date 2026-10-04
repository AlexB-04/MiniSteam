using Microsoft.Extensions.Diagnostics.HealthChecks;
using MiniSteam.Data;

namespace MiniSteam.Helpers
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public DatabaseHealthCheck(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<DataContext>();

                return await dataContext.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy("Database connection is available.")
                    : HealthCheckResult.Unhealthy("Database connection is unavailable.");
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy(
                    "Database health check failed.",
                    exception);
            }
        }
    }
}
