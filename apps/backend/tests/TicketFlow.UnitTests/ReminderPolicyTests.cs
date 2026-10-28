using TicketFlow.Application.Notifications;
using TicketFlow.Domain.Tickets;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class ReminderPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Reminder_is_only_for_waiting_customer_after_threshold()
    {
        Assert.Equal(ReminderKind.CustomerReminder, ReminderPolicy.Evaluate(TicketStatus.WaitingForCustomer, Now.AddHours(-25), Now, 24, 48));
        Assert.Null(ReminderPolicy.Evaluate(TicketStatus.WaitingForCustomer, Now.AddHours(-23), Now, 24, 48));
    }

    [Theory]
    [InlineData(TicketStatus.Open)]
    [InlineData(TicketStatus.InProgress)]
    public void Escalation_is_only_for_active_tickets_after_threshold(TicketStatus status)
    {
        Assert.Equal(ReminderKind.StaffEscalation, ReminderPolicy.Evaluate(status, Now.AddHours(-49), Now, 24, 48));
        Assert.Null(ReminderPolicy.Evaluate(status, Now.AddHours(-47), Now, 24, 48));
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Finished_tickets_are_never_reminded(TicketStatus status) =>
        Assert.Null(ReminderPolicy.Evaluate(status, Now.AddDays(-20), Now, 24, 48));
}
