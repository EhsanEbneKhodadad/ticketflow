namespace TicketFlow.Domain.Tickets;

public sealed record TicketAttachment(Guid Id, Guid TicketId, Guid? MessageId, string FileName,
    string ContentType, long Size, string ObjectKey, bool Internal, Guid UploadedBy, DateTimeOffset CreatedAt);
