using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TicketFlow.Application.Notifications;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Notifications;

public sealed class ReminderScheduler(TicketFlowDbContext db, IConfiguration config)
{
    public async Task EnqueueDueAsync(CancellationToken ct)
    {
        var reminderHours = config.GetValue("Jobs:ReminderHours", 24);
        var escalationHours = config.GetValue("Jobs:EscalationHours", 48);
        if (reminderHours <= 0 || escalationHours <= 0) throw new InvalidOperationException("Reminder and escalation hours must be positive.");
        var now = DateTimeOffset.UtcNow;
        var batch = await db.Tickets.AsNoTracking().Where(t =>
                ((t.Status == TicketStatus.WaitingForCustomer && t.UpdatedAt <= now.AddHours(-reminderHours)) ||
                 ((t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress) && t.UpdatedAt <= now.AddHours(-escalationHours))) &&
                !db.OutboxMessages.Any(m => m.TicketId == t.Id && m.TicketVersion == t.Version &&
                    (m.Kind == "customerReminder" || m.Kind == "staffEscalation")))
            .OrderBy(t => t.Number).Take(1000).ToArrayAsync(ct);
        foreach (var ticket in batch)
        {
            var kind = ReminderPolicy.Evaluate(ticket.Status, ticket.UpdatedAt, now, reminderHours, escalationHours) == ReminderKind.CustomerReminder
                ? "customerReminder" : "staffEscalation";
            var key = $"{ticket.Id:N}:{ticket.Version}:{kind}";
            var id = Guid.NewGuid();
            var actor = Guid.Empty;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO outbox_messages
                ("Id", "TicketId", "ActorId", "RequesterId", "AgentId", "Kind", "DedupKey", "TicketVersion", "Internal", "CreatedAt", "Attempts")
                VALUES ({id}, {ticket.Id}, {actor}, {ticket.RequesterId}, {ticket.AgentId}, {kind}, {key}, {ticket.Version}, {false}, {now}, {0})
                ON CONFLICT ("DedupKey") DO NOTHING
                """, ct);
        }
    }
}
