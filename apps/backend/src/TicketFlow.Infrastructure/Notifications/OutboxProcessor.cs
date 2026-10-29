using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Notifications;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Notifications;

public sealed class OutboxProcessor(TicketFlowDbContext db, INotificationSender sender, ILogger<OutboxProcessor> logger)
{
    public async Task<bool> DispatchOneAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var lease = Guid.NewGuid();
        var until = now.AddMinutes(2);
        var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE outbox_messages SET "LeaseId" = {lease}, "LeaseUntil" = {until}, "Attempts" = "Attempts" + 1
            WHERE "Id" = (SELECT "Id" FROM outbox_messages
              WHERE "CompletedAt" IS NULL AND "FailedAt" IS NULL
                AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
                AND ("LeaseUntil" IS NULL OR "LeaseUntil" <= {now})
              ORDER BY "CreatedAt", "Id" FOR UPDATE SKIP LOCKED LIMIT 1)
            """, ct);
        if (claimed == 0) return false;
        var item = await db.OutboxMessages.SingleAsync(m => m.LeaseId == lease, ct);
        try
        {
            var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == item.TicketId, ct);
            if (ticket is not null)
            {
                var recipients = NotificationPolicy.Recipients(item.Kind, item.Internal, item.ActorId, item.RequesterId, item.AgentId);
                if (item.Kind is "customerReminder" or "staffEscalation")
                {
                    var eligible = item.TicketVersion == ticket.Version && (item.Kind == "customerReminder"
                        ? ticket.Status == TicketStatus.WaitingForCustomer
                        : ticket.Status is TicketStatus.Open or TicketStatus.InProgress);
                    recipients = eligible
                        ? item.Kind == "customerReminder" ? [ticket.RequesterId] : ticket.AgentId.HasValue ? [ticket.AgentId.Value] : []
                        : [];
                    if (eligible && item.Kind == "staffEscalation" && recipients.Count > 0 && !await IsStaffAsync(recipients[0], ct))
                        recipients = [];
                    if (eligible && item.Kind == "staffEscalation" && recipients.Count == 0)
                    {
                        var admin = await (from userRole in db.UserRoles
                                           join role in db.Roles on userRole.RoleId equals role.Id
                                           where role.Name == "Admin"
                                           orderby userRole.UserId
                                           select (Guid?)userRole.UserId).FirstOrDefaultAsync(ct);
                        if (admin.HasValue) recipients = [admin.Value];
                    }
                }
                foreach (var id in recipients)
                {
                    if (id != ticket.RequesterId && !await IsStaffAsync(id, ct)) continue;
                    var address = await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Email).SingleOrDefaultAsync(ct);
                    if (!string.IsNullOrWhiteSpace(address)) await sender.SendAsync(address, ticket.Id, ticket.Number, item.Kind, item.Id, ct);
                }
            }
            item.CompletedAt = DateTimeOffset.UtcNow;
            item.LeaseId = null;
            item.LeaseUntil = null;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            // Email is at-least-once: a crash after SMTP acceptance can cause duplicate delivery.
            logger.LogWarning("Outbox delivery failed ({FailureType}); event {EventId} will be retried or held for replay.", error.GetType().Name, item.Id);
            item.LeaseId = null;
            item.LeaseUntil = null;
            if (item.Attempts >= 5) item.FailedAt = DateTimeOffset.UtcNow;
            else item.NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(Math.Pow(2, item.Attempts));
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    private Task<bool> IsStaffAsync(Guid id, CancellationToken ct) =>
        (from userRole in db.UserRoles
         join role in db.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == id && (role.Name == "Agent" || role.Name == "Admin")
         select userRole.UserId).AnyAsync(ct);

    public async Task<bool> ReplayAsync(Guid id, CancellationToken ct) =>
        await db.OutboxMessages.Where(m => m.Id == id && m.FailedAt != null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.FailedAt, (DateTimeOffset?)null)
                .SetProperty(m => m.NextAttemptAt, (DateTimeOffset?)null)
                .SetProperty(m => m.Attempts, 0)
                .SetProperty(m => m.LeaseId, (Guid?)null)
                .SetProperty(m => m.LeaseUntil, (DateTimeOffset?)null), ct) == 1;
}
