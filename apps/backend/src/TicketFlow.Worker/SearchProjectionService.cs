using TicketFlow.Application.Search;
using TicketFlow.Infrastructure.Search;

namespace TicketFlow.Worker;

public sealed class SearchProjectionService(IServiceScopeFactory scopes, SearchSettings settings, ILogger<SearchProjectionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!settings.Enabled)
        {
            logger.LogInformation("Search projection is disabled.");
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SearchProjectionWorkflow>().ReconcileAsync(stoppingToken);
                logger.LogInformation("Search reconciliation completed.");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                logger.LogError("Search reconciliation failed ({FailureType}); retrying after {IntervalSeconds} seconds.", error.GetType().Name, settings.IntervalSeconds);
            }
            await Task.Delay(TimeSpan.FromSeconds(settings.IntervalSeconds), stoppingToken);
        }
    }
}
