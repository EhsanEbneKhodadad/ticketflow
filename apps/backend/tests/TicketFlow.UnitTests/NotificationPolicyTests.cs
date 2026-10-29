using TicketFlow.Application.Notifications;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class NotificationPolicyTests
{
    private readonly Guid requester = Guid.NewGuid();
    private readonly Guid agent = Guid.NewGuid();

    [Fact]
    public void Internal_note_never_reaches_customer()
    {
        var recipients = NotificationPolicy.Recipients("internalNoteAdded", true, agent, requester, agent);
        Assert.DoesNotContain(requester, recipients);
    }

    [Fact]
    public void Public_reply_goes_to_other_participant_and_never_actor()
    {
        Assert.Equal(new[] { agent }, NotificationPolicy.Recipients("replyAdded", false, requester, requester, agent));
        Assert.Equal(new[] { requester }, NotificationPolicy.Recipients("replyAdded", false, agent, requester, agent));
    }

    [Fact]
    public void Assignment_notifies_new_agent_but_unrelated_changes_do_not_send()
    {
        var admin = Guid.NewGuid();
        Assert.Equal(new[] { agent }, NotificationPolicy.Recipients("assigned", false, admin, requester, agent));
        Assert.Empty(NotificationPolicy.Recipients("edited", false, requester, requester, agent));
        Assert.Empty(NotificationPolicy.Recipients("assigned", false, agent, requester, agent));
    }
}
