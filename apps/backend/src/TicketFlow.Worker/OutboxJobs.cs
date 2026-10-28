using Hangfire;
using TicketFlow.Infrastructure.Notifications;

namespace TicketFlow.Worker;

public sealed class OutboxJobs(OutboxProcessor processor, ReminderScheduler reminders)
{
    [AutomaticRetry(Attempts = 3)]
    public async Task RunAsync(CancellationToken ct)
    {
        await reminders.EnqueueDueAsync(ct);
        for (var i = 0; i < 100; i++)
            if (!await processor.DispatchOneAsync(ct)) break;
    }
}

public sealed class OutboxSchedule(IRecurringJobManager recurring) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        recurring.AddOrUpdate<OutboxJobs>("ticketflow-outbox", job => job.RunAsync(CancellationToken.None), "* * * * *");
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
