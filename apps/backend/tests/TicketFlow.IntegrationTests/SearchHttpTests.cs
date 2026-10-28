using System.Net;
using System.Text.Json;
using TicketFlow.Application.Search;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Search;
using Xunit;

namespace TicketFlow.IntegrationTests;

// HTTP boundary tests; these do not replace the real Elasticsearch container suite.
public sealed class SearchHttpTests
{
    private static readonly Actor Customer = new(Guid.NewGuid(), "Customer", "Customer");
    private static readonly SearchSettings Enabled = new(true, new Uri("http://localhost:9200"), "test-index", 15);

    [Fact]
    public async Task Search_scopes_customer_candidates_caps_results_and_ignores_index_source()
    {
        var id = Guid.NewGuid();
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            Assert.Equal("http://localhost:9200/test-index/_search", request.RequestUri?.AbsoluteUri);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var root = body.RootElement;
            Assert.False(root.GetProperty("_source").GetBoolean());
            Assert.False(root.GetProperty("track_total_hits").GetBoolean());
            Assert.Equal(1000, root.GetProperty("size").GetInt32());
            Assert.Equal(Customer.Id.ToString(), root.GetProperty("query").GetProperty("bool").GetProperty("filter")[0].GetProperty("term").GetProperty("requesterId").GetString());
            Assert.Contains("rocket", root.GetProperty("query").GetRawText());
            return Response(HttpStatusCode.OK, "{\"timed_out\":false,\"_shards\":{\"failed\":0},\"hits\":{\"total\":{\"value\":9999},\"hits\":[{\"_id\":\"" + id + "\",\"_source\":{\"description\":\"UNTRUSTED secret\"}}]}}");
        }));
        var result = await new ElasticsearchTicketIndex(http, Enabled).SearchAsync(Customer, new(Q: "rocket"), default);
        Assert.Equal(id, Assert.Single(result));
    }

    [Fact]
    public async Task Search_prefilters_all_metadata_before_applying_the_candidate_window()
    {
        var category = Guid.NewGuid();
        var agent = Guid.NewGuid();
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var filters = body.RootElement.GetProperty("query").GetProperty("bool").GetProperty("filter").EnumerateArray().ToArray();
            Assert.Equal(6, filters.Length);
            var terms = filters.Where(f => f.TryGetProperty("term", out _)).SelectMany(f => f.GetProperty("term").EnumerateObject()).ToDictionary(p => p.Name, p => p.Value.GetString());
            Assert.Equal("open", terms["status"]);
            Assert.Equal("high", terms["priority"]);
            Assert.Equal(category.ToString(), terms["categoryId"]);
            Assert.Equal(agent.ToString(), terms["agentId"]);
            var range = filters.Single(f => f.TryGetProperty("range", out _)).GetProperty("range").GetProperty("createdAt");
            Assert.Equal(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), range.GetProperty("gte").GetDateTimeOffset());
            Assert.Equal(new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero), range.GetProperty("lt").GetDateTimeOffset());
            return Response(HttpStatusCode.OK, "{\"timed_out\":false,\"_shards\":{\"failed\":0},\"hits\":{\"hits\":[]}}");
        }));
        await new ElasticsearchTicketIndex(http, Enabled).SearchAsync(Customer,
            new(Q: "rocket", Status: TicketStatus.Open, Priority: TicketPriority.High, CategoryId: category, AgentId: agent,
                From: new(2026, 10, 1), To: new(2026, 10, 28)), default);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("network")]
    [InlineData("timeout")]
    [InlineData("partial")]
    [InlineData("timed_out")]
    [InlineData("missing-index")]
    [InlineData("malformed")]
    public async Task Unavailable_or_partial_search_never_returns_a_successful_empty_page(string failure)
    {
        using var http = new HttpClient(new Handler((_, _) => failure switch
        {
            "network" => throw new HttpRequestException("secret endpoint"),
            "timeout" => throw new TaskCanceledException("secret endpoint"),
            "status" => Task.FromResult(Response(HttpStatusCode.ServiceUnavailable, "secret backend")),
            "missing-index" => Task.FromResult(Response(HttpStatusCode.NotFound, "secret missing index")),
            "timed_out" => Task.FromResult(Response(HttpStatusCode.OK, "{\"timed_out\":true,\"_shards\":{\"failed\":0},\"hits\":{\"hits\":[]}}")),
            "partial" => Task.FromResult(Response(HttpStatusCode.OK, "{\"timed_out\":false,\"_shards\":{\"failed\":1},\"hits\":{\"hits\":[]}}")),
            _ => Task.FromResult(Response(HttpStatusCode.OK, "invalid json"))
        }));
        var error = await Assert.ThrowsAsync<RuleException>(() => new ElasticsearchTicketIndex(http, Enabled).SearchAsync(Customer, new(), default));
        Assert.Equal(ErrorKind.Unavailable, error.Kind);
        Assert.DoesNotContain("secret", error.Message);
    }

    [Fact]
    public async Task Disabled_search_does_not_contact_elasticsearch_and_caller_cancellation_is_preserved()
    {
        var requests = 0;
        using var http = new HttpClient(new Handler((_, ct) => { requests++; throw new OperationCanceledException(ct); }));
        var disabled = new ElasticsearchTicketIndex(http, Enabled with { Enabled = false });
        Assert.Equal(ErrorKind.Unavailable, (await Assert.ThrowsAsync<RuleException>(() => disabled.SearchAsync(Customer, new(), default))).Kind);
        Assert.Equal(0, requests);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ElasticsearchTicketIndex(http, Enabled).SearchAsync(Customer, new(), cancellation.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Bootstrap_creates_strict_public_mapping_and_tolerates_concurrent_creation(bool race)
    {
        var requests = 0;
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            requests++;
            Assert.Equal("/test-index", request.RequestUri?.AbsolutePath);
            if (request.Method == HttpMethod.Head) return Response(HttpStatusCode.NotFound, "");
            Assert.Equal(HttpMethod.Put, request.Method);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal("strict", body.RootElement.GetProperty("mappings").GetProperty("dynamic").GetString());
            var fields = body.RootElement.GetProperty("mappings").GetProperty("properties");
            Assert.Equal("keyword", fields.GetProperty("requesterId").GetProperty("type").GetString());
            Assert.Equal("text", fields.GetProperty("publicReplies").GetProperty("type").GetString());
            Assert.False(fields.TryGetProperty("internalNotes", out _));
            Assert.False(fields.TryGetProperty("attachments", out _));
            return race ? Response(HttpStatusCode.BadRequest, "{\"error\":{\"type\":\"resource_already_exists_exception\"}}")
                : Response(HttpStatusCode.OK, "{\"acknowledged\":true}");
        }));
        await new ElasticsearchTicketIndex(http, Enabled).EnsureIndexAsync(default);
        Assert.Equal(2, requests);
    }

    [Fact]
    public async Task Partial_bulk_failure_is_reported_instead_of_losing_projection_updates()
    {
        using var http = new HttpClient(new Handler(async (request, ct) =>
        {
            Assert.EndsWith("/_bulk?refresh=wait_for", request.RequestUri?.AbsoluteUri);
            Assert.Equal("application/x-ndjson", request.Content!.Headers.ContentType?.MediaType);
            var body = await request.Content.ReadAsStringAsync(ct);
            Assert.EndsWith("\n", body);
            Assert.Contains("\"_id\":\"", body);
            return Response(HttpStatusCode.OK, "{\"errors\":true,\"items\":[{\"index\":{\"status\":400}}]}");
        }));
        var doc = new PublicTicketDocument(Guid.NewGuid(), 1001, "TKT-1001", "Title", "Description", [], [], Customer.Id, DateTimeOffset.UtcNow, TicketStatus.Open, TicketPriority.Medium, Guid.NewGuid(), null);
        var error = await Assert.ThrowsAsync<RuleException>(() => new ElasticsearchTicketIndex(http, Enabled).UpsertAsync([doc], default));
        Assert.Equal(ErrorKind.Unavailable, error.Kind);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string json) => new(status) { Content = new StringContent(json) };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
