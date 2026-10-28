using TicketFlow.Domain.Tickets;

namespace TicketFlow.Application.Notifications;

public enum ReminderKind { CustomerReminder, StaffEscalation }

public static class ReminderPolicy
{
    public static ReminderKind? Evaluate(TicketStatus status, DateTimeOffset updated, DateTimeOffset now, int reminderHours, int escalationHours)
    {
        if (reminderHours <= 0 || escalationHours <= 0) throw new ArgumentOutOfRangeException(nameof(reminderHours));
        if (status == TicketStatus.WaitingForCustomer && updated <= now.AddHours(-reminderHours)) return ReminderKind.CustomerReminder;
        if (status is TicketStatus.Open or TicketStatus.InProgress && updated <= now.AddHours(-escalationHours)) return ReminderKind.StaffEscalation;
        return null;
    }
}
