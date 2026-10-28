namespace TicketFlow.Application.Notifications;

public static class NotificationPolicy
{
    public static IReadOnlyList<Guid> Recipients(string kind, bool internalEvent, Guid actorId, Guid requesterId, Guid? agentId)
    {
        if (internalEvent) return [];
        Guid? recipient = kind switch
        {
            "replyAdded" when actorId == requesterId => agentId,
            "replyAdded" => requesterId,
            "assigned" => agentId,
            "statusChanged" when actorId != requesterId => requesterId,
            _ => null
        };
        return recipient.HasValue && recipient.Value != actorId ? [recipient.Value] : [];
    }
}
