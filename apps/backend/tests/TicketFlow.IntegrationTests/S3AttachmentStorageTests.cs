using System.Net;
using System.Text;
using DotNet.Testcontainers.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TicketFlow.Infrastructure.Attachments;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class S3AttachmentStorageTests
{
    [Fact]
    public async Task Actual_s3_adapter_round_trips_private_bytes_and_deletes_objects()
    {
        var endpoint = Environment.GetEnvironmentVariable("TICKETFLOW_TEST_S3_ENDPOINT");
        await using var container = string.IsNullOrWhiteSpace(endpoint)
            ? new ContainerBuilder("chrislusf/seaweedfs:4.51")
                .WithCommand("mini", "-dir=/data").WithPortBinding(8333, true)
                .WithEnvironment("AWS_ACCESS_KEY_ID", "ticketflow")
                .WithEnvironment("AWS_SECRET_ACCESS_KEY", "ticketflow_storage_local_only")
                .WithEnvironment("S3_BUCKET", "ticketflow-attachments")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(8333)).Build()
            : null;
        if (container is not null)
        {
            await container.StartAsync();
            endpoint = $"http://{container.Hostname}:{container.GetMappedPublicPort(8333)}";
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Endpoint"] = endpoint,
            ["Storage:Bucket"] = "ticketflow-attachments",
            ["Storage:AccessKey"] = Environment.GetEnvironmentVariable("TICKETFLOW_TEST_S3_ACCESS_KEY") ?? "ticketflow",
            ["Storage:SecretKey"] = Environment.GetEnvironmentVariable("TICKETFLOW_TEST_S3_SECRET_KEY") ?? "ticketflow_storage_local_only"
        }).Build();
        using var storage = new S3AttachmentStorage(config, NullLogger<S3AttachmentStorage>.Instance);
        var key = "integration-tests/" + Guid.NewGuid().ToString("N");
        var bytes = Encoding.UTF8.GetBytes("Private attachment data \u2603");
        try
        {
            await storage.UploadAsync(key, bytes, "text/plain", CancellationToken.None);
            Assert.Equal(bytes, await storage.DownloadAsync(key, CancellationToken.None));
            using var anonymous = new HttpClient();
            var response = await anonymous.GetAsync(endpoint + "/ticketflow-attachments/" + key);
            Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
        }
        finally { await storage.DeleteAsync(key, CancellationToken.None); }
        await Assert.ThrowsAsync<TicketFlow.Domain.Common.RuleException>(() => storage.DownloadAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Unavailable_s3_returns_generic_application_error_without_private_key_or_vendor_details()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Endpoint"] = "http://127.0.0.1:1",
            ["Storage:AccessKey"] = "test",
            ["Storage:SecretKey"] = "test"
        }).Build();
        using var storage = new S3AttachmentStorage(config, NullLogger<S3AttachmentStorage>.Instance);
        var error = await Assert.ThrowsAsync<TicketFlow.Domain.Common.RuleException>(() => storage.UploadAsync("private-sensitive-key", "details"u8.ToArray(), "text/plain", CancellationToken.None));
        Assert.Equal(TicketFlow.Domain.Common.ErrorKind.Unavailable, error.Kind);
        Assert.DoesNotContain("private-sensitive-key", error.Message);
        Assert.DoesNotContain("Amazon", error.Message);
        Assert.Null(error.InnerException);
    }
}
