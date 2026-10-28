using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TicketFlow.Application.Attachments;
using TicketFlow.Domain.Common;

namespace TicketFlow.Infrastructure.Attachments;

public sealed class S3AttachmentStorage : IAttachmentStorage, IDisposable
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;
    private readonly ILogger<S3AttachmentStorage> _logger;

    public S3AttachmentStorage(IConfiguration configuration, ILogger<S3AttachmentStorage> logger)
    {
        _logger = logger;
        var section = configuration.GetSection("Storage");
        _bucket = section["Bucket"] ?? "ticketflow-attachments";
        var config = new AmazonS3Config { AuthenticationRegion = section["Region"] ?? "us-east-1", ForcePathStyle = true, MaxErrorRetry = 2, Timeout = TimeSpan.FromSeconds(30) };
        if (!string.IsNullOrWhiteSpace(section["Endpoint"])) config.ServiceURL = section["Endpoint"];
        else config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(section["Region"] ?? "us-east-1");
        if (!string.IsNullOrWhiteSpace(section["AccessKey"]) && !string.IsNullOrWhiteSpace(section["SecretKey"]))
            _client = new AmazonS3Client(new BasicAWSCredentials(section["AccessKey"], section["SecretKey"]), config);
        else _client = new AmazonS3Client(config);
    }

    public async Task UploadAsync(string key, byte[] bytes, string contentType, CancellationToken ct)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        try
        {
            await _client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = stream,
                ContentType = contentType,
                AutoCloseStream = false,
                UseChunkEncoding = false
            }, ct);
        }
        catch (Exception error) when (StorageFailure(error, ct)) { throw Unavailable(); }
    }

    public async Task<byte[]> DownloadAsync(string key, CancellationToken ct)
    {
        try
        {
            using var response = await _client.GetObjectAsync(_bucket, key, ct);
            if (response.ContentLength > AttachmentPolicy.MaxFileBytes) throw Unavailable();
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await response.ResponseStream.ReadAsync(buffer, ct)) > 0)
            {
                if (output.Length + read > AttachmentPolicy.MaxFileBytes) throw Unavailable();
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            return output.ToArray();
        }
        catch (AmazonS3Exception error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound)
        { throw new RuleException(ErrorKind.NotFound, "Attachment content not found."); }
        catch (Exception error) when (StorageFailure(error, ct)) { throw Unavailable(); }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        try { await _client.DeleteObjectAsync(_bucket, key, ct); }
        catch
        {
            // Do not log private storage keys or credentials from SDK exception messages.
            _logger.LogError("Attachment object cleanup failed; storage reconciliation is required.");
            throw;
        }
    }

    private static bool StorageFailure(Exception error, CancellationToken ct) =>
        error is AmazonServiceException or AmazonClientException or HttpRequestException or IOException
        || (error is OperationCanceledException && !ct.IsCancellationRequested);

    private RuleException Unavailable()
    {
        _logger.LogWarning("Attachment storage is unavailable.");
        return new RuleException(ErrorKind.Unavailable, "Attachment storage is temporarily unavailable. Please try again later.");
    }

    public void Dispose() => _client.Dispose();
}
