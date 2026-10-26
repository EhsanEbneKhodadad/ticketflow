namespace TicketFlow.Worker;

public sealed class WorkerLifecycle(ILogger<WorkerLifecycle> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TicketFlow worker foundation started. Background jobs are not configured yet.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TicketFlow worker stopped.");
        return Task.CompletedTask;
    }
}
