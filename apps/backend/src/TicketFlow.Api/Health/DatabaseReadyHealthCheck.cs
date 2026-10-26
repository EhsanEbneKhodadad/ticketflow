using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Api.Health;

public sealed class DatabaseReadyHealthCheck(TicketFlowDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Unhealthy("Database unavailable.");

            var pending = await db.Database.GetPendingMigrationsAsync(cancellationToken);
            return pending.Any()
                ? HealthCheckResult.Unhealthy("Database migrations pending.")
                : HealthCheckResult.Healthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Database readiness failed.");
        }
    }
}
