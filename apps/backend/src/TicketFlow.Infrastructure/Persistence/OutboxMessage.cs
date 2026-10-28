namespace TicketFlow.Infrastructure.Persistence;

public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid ActorId { get; set; }
    public Guid RequesterId { get; set; }
    public Guid? AgentId { get; set; }
    public string Kind { get; set; } = "";
    public string? DedupKey { get; set; }
    public int? TicketVersion { get; set; }
    public bool Internal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
}
