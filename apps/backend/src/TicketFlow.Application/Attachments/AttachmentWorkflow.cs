using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Application.Attachments;

public sealed class AttachmentWorkflow(ITicketStore store, IAttachmentStorage storage)
{
    public async Task<IReadOnlyList<AttachmentView>> ListAsync(Actor actor, Guid id, CancellationToken ct)
    {
        var ticket = await FindAsync(actor, id, ct);
        return ticket.Attachments.Where(a => actor.IsStaff || !a.Internal).OrderBy(a => a.CreatedAt)
            .Select(a => View(a, ticket.Version)).ToArray();
    }

    public async Task<AttachmentView> UploadAsync(Actor actor, Guid id, string fileName, byte[] bytes, int expectedVersion, Guid? messageId, CancellationToken ct)
    {
        var ticket = await FindAsync(actor, id, ct);
        var validated = AttachmentPolicy.Validate(fileName, bytes);
        var key = $"attachments/{Guid.NewGuid():N}/{Guid.NewGuid():N}";
        var attachment = ticket.Attach(actor, validated.FileName, validated.ContentType, bytes.LongLength, key, messageId, expectedVersion);
        try
        {
            await storage.UploadAsync(key, bytes, attachment.ContentType, ct);
            await store.SaveAsync(ct);
        }
        catch
        {
            // Cleanup has its own deadline so a canceled HTTP request cannot strand an uploaded object.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await storage.DeleteAsync(key, cleanup.Token); }
            catch { /* The storage adapter logs cleanup failures; preserve the original failure. */ }
            throw;
        }
        return View(attachment, ticket.Version);
    }

    public async Task<AttachmentDownload> DownloadAsync(Actor actor, Guid id, Guid attachmentId, CancellationToken ct)
    {
        var ticket = await FindAsync(actor, id, ct);
        var attachment = ticket.Attachments.SingleOrDefault(a => a.Id == attachmentId && (actor.IsStaff || !a.Internal))
            ?? throw new RuleException(ErrorKind.NotFound, "Attachment not found.");
        return new AttachmentDownload(await storage.DownloadAsync(attachment.ObjectKey, ct), attachment.FileName, attachment.ContentType);
    }

    private async Task<Ticket> FindAsync(Actor actor, Guid id, CancellationToken ct)
    {
        var ticket = await store.FindAsync(id, ct);
        if (ticket is null || (!actor.IsStaff && actor.Id != ticket.RequesterId))
            throw new RuleException(ErrorKind.NotFound, "Ticket not found.");
        return ticket;
    }

    private static AttachmentView View(TicketAttachment a, int version) =>
        new(a.Id, a.TicketId, a.MessageId, a.FileName, a.ContentType, a.Size, a.Internal, a.UploadedBy, a.CreatedAt, version);
}
