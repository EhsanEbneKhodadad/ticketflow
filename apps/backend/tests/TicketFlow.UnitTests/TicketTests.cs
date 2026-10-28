using TicketFlow.Domain.Tickets;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class TicketTests
{
    private static readonly Actor Customer = new(Guid.NewGuid(), "Customer", "Customer");
    private static readonly Actor Agent = new(Guid.NewGuid(), "Agent", "Agent");
    private static Ticket NewTicket() => Ticket.Create(Customer, "Cannot sign in", "My account rejects the correct password.", Guid.NewGuid(), [" Login ", "login"], TicketPriority.High);

    [Fact]
    public void Creation_normalizes_input_and_records_initial_history()
    {
        var ticket = NewTicket();
        Assert.Equal("Cannot sign in", ticket.Title);
        Assert.Equal(new[] { "login" }, ticket.Tags);
        Assert.Equal(1, ticket.Version);
        Assert.Equal(Customer.Id, ticket.RequesterId);
        Assert.Equal("created", Assert.Single(ticket.Audit).Kind);
    }

    [Fact]
    public void Staff_can_resolve_customer_can_close_and_reopen()
    {
        var ticket = NewTicket();
        ticket.ChangeStatus(Agent, TicketStatus.InProgress, 1);
        ticket.ChangeStatus(Agent, TicketStatus.Resolved, 2);
        ticket.ChangeStatus(Customer, TicketStatus.Closed, 3);
        ticket.ChangeStatus(Customer, TicketStatus.Open, 4);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(5, ticket.Version);
        Assert.Equal(5, ticket.Audit.Count);
    }

    [Fact]
    public void Customer_cannot_add_internal_note_or_manage_priority()
    {
        var ticket = NewTicket();
        Assert.Equal(ErrorKind.Forbidden, Assert.Throws<RuleException>(() => ticket.Reply(Customer, "Private note", true, 1)).Kind);
        Assert.Equal(ErrorKind.Forbidden, Assert.Throws<RuleException>(() => ticket.ChangePriority(Customer, TicketPriority.Urgent, 1)).Kind);
        Assert.Empty(ticket.Messages);
    }

    [Fact]
    public void Foreign_customer_cannot_mutate_ticket()
    {
        var ticket = NewTicket();
        var other = new Actor(Guid.NewGuid(), "Other", "Customer");
        Assert.Equal(ErrorKind.Forbidden, Assert.Throws<RuleException>(() => ticket.Reply(other, "Hello", false, 1)).Kind);
    }

    [Fact]
    public void Stale_version_cannot_overwrite_a_change()
    {
        var ticket = NewTicket();
        ticket.ChangePriority(Agent, TicketPriority.Urgent, 1);
        Assert.Equal(ErrorKind.Conflict, Assert.Throws<RuleException>(() => ticket.ChangeStatus(Agent, TicketStatus.Resolved, 1)).Kind);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(2, ticket.Version);
    }

    [Fact]
    public void Internal_note_has_private_history_and_public_reply_has_public_history()
    {
        var ticket = NewTicket();
        ticket.Reply(Agent, "Internal troubleshooting", true, 1);
        ticket.Reply(Customer, "Public details", false, 2);
        Assert.True(ticket.Messages[0].Internal);
        Assert.True(ticket.Audit[1].Internal);
        Assert.False(ticket.Messages[1].Internal);
        Assert.False(ticket.Audit[2].Internal);
    }

    [Fact]
    public void Invalid_transition_and_reply_to_closed_ticket_are_rejected()
    {
        var ticket = NewTicket();
        Assert.Equal(ErrorKind.Conflict, Assert.Throws<RuleException>(() => ticket.ChangeStatus(Agent, TicketStatus.Closed, 1)).Kind);
        ticket.ChangeStatus(Agent, TicketStatus.Resolved, 1);
        ticket.ChangeStatus(Customer, TicketStatus.Closed, 2);
        Assert.Equal(ErrorKind.Conflict, Assert.Throws<RuleException>(() => ticket.Reply(Customer, "Another reply", false, 3)).Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void Invalid_title_is_rejected(string title) =>
        Assert.Equal(ErrorKind.Validation, Assert.Throws<RuleException>(() => Ticket.Create(Customer, title, "A sufficiently detailed description.", Guid.NewGuid(), [], TicketPriority.Low)).Kind);
}
