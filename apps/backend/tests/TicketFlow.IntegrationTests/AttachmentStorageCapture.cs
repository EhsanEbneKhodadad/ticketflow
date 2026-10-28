using System.Collections.Concurrent;
using TicketFlow.Application.Attachments;

namespace TicketFlow.IntegrationTests;

public sealed class AttachmentStorageCapture : IAttachmentStorage
{
    public ConcurrentDictionary<string, byte[]> Objects { get; } = new();
    public int Deletes;
    public Func<Task>? BeforeUpload { get; set; }

    public async Task UploadAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        if (BeforeUpload is not null) await BeforeUpload();
        Objects[key] = bytes;
    }

    public Task<byte[]> DownloadAsync(string key, CancellationToken ct) => Task.FromResult(Objects[key]);
    public Task DeleteAsync(string key, CancellationToken ct)
    {
        Interlocked.Increment(ref Deletes);
        Objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
