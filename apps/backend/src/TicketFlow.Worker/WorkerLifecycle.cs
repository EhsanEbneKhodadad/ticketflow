namespace TicketFlow.Worker;

public sealed class WorkerLifecycle(ILogger<WorkerLifecycle> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TicketFlow worker started.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("TicketFlow worker stopped.");
        return Task.CompletedTask;
    }
}
