namespace TicketFlow.Application.Attachments;

public sealed record AttachmentView(Guid Id, Guid TicketId, Guid? MessageId, string FileName, string ContentType,
    long Size, bool Internal, Guid UploadedBy, DateTimeOffset CreatedAt, int TicketVersion);
public sealed record AttachmentDownload(byte[] Bytes, string FileName, string ContentType);
public interface IAttachmentStorage
{
    Task UploadAsync(string key, byte[] bytes, string contentType, CancellationToken ct);
    Task<byte[]> DownloadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}
