using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TicketFlow.Application.Search;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Infrastructure.Search;

public sealed class ElasticsearchTicketIndex(HttpClient http, SearchSettings settings) : ITicketSearchIndex
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public Task<IReadOnlyList<Guid>> SearchAsync(Actor actor, TicketSearchQuery query, CancellationToken ct) => ExecuteAsync<IReadOnlyList<Guid>>(async () =>
    {
        var text = query.Q?.Trim();
        object match = string.IsNullOrEmpty(text) ? new { match_all = new { } } : new
        {
            @bool = new
            {
                should = new object[] {
                    new { multi_match = new { query = text, fields = new[] { "title^3", "description", "tags^2", "publicReplies" }, type = "cross_fields", @operator = "and" } },
                    new { term = new { ticketNumber = text.StartsWith("TKT-", StringComparison.OrdinalIgnoreCase) ? text.ToUpperInvariant() : "TKT-" + text } }
                },
                minimum_should_match = 1
            }
        };
        var filters = new List<object>();
        if (!actor.IsStaff) filters.Add(new { term = new { requesterId = actor.Id } });
        if (query.Status.HasValue) filters.Add(new { term = new { status = query.Status.Value } });
        if (query.Priority.HasValue) filters.Add(new { term = new { priority = query.Priority.Value } });
        if (query.CategoryId.HasValue) filters.Add(new { term = new { categoryId = query.CategoryId.Value } });
        if (query.AgentId.HasValue) filters.Add(new { term = new { agentId = query.AgentId.Value } });
        var dates = new Dictionary<string, DateTimeOffset>();
        if (query.From.HasValue) dates["gte"] = new(query.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (query.To.HasValue && query.To.Value < DateOnly.MaxValue)
            dates["lt"] = new(query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (dates.Count != 0) filters.Add(new { range = new { createdAt = dates } });
        var sort = new List<object>();
        if (query.Sort == SearchSort.Relevance) sort.Add(new { _score = "desc" });
        sort.Add(new { createdAt = "desc" });
        sort.Add(new { number = "desc" });
        using var response = await SendAsync(HttpMethod.Post, "/_search", Body(new
        {
            size = TicketSearchWorkflow.CandidateLimit,
            _source = false,
            track_total_hits = false,
            query = new { @bool = new { must = new[] { match }, filter = filters } },
            sort
        }), ct);
        Success(response);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = result.RootElement;
        if (root.GetProperty("timed_out").GetBoolean() || root.GetProperty("_shards").GetProperty("failed").GetInt32() != 0)
            throw Unavailable();
        return root.GetProperty("hits").GetProperty("hits").EnumerateArray().Take(TicketSearchWorkflow.CandidateLimit)
            .Select(hit => Guid.Parse(hit.GetProperty("_id").GetString()!)).ToArray();
    }, ct);

    public Task EnsureIndexAsync(CancellationToken ct) => ExecuteAsync(async () =>
    {
        using var exists = await SendAsync(HttpMethod.Head, "", null, ct);
        if (exists.IsSuccessStatusCode) return true;
        if (exists.StatusCode != HttpStatusCode.NotFound) throw Unavailable();
        var properties = new Dictionary<string, object>
        {
            ["id"] = new { type = "keyword" },
            ["number"] = new { type = "long" },
            ["ticketNumber"] = new { type = "keyword" },
            ["title"] = new { type = "text" },
            ["description"] = new { type = "text" },
            ["tags"] = new { type = "text" },
            ["publicReplies"] = new { type = "text" },
            ["requesterId"] = new { type = "keyword" },
            ["createdAt"] = new { type = "date" },
            ["status"] = new { type = "keyword" },
            ["priority"] = new { type = "keyword" },
            ["categoryId"] = new { type = "keyword" },
            ["agentId"] = new { type = "keyword" }
        };
        using var create = await SendAsync(HttpMethod.Put, "", Body(new
        {
            settings = new { number_of_shards = 1, number_of_replicas = 0 },
            mappings = new { dynamic = "strict", properties }
        }), ct);
        using var result = JsonDocument.Parse(await create.Content.ReadAsStringAsync(ct));
        if (create.StatusCode == HttpStatusCode.BadRequest && result.RootElement.GetProperty("error").GetProperty("type").GetString() == "resource_already_exists_exception")
            return true;
        Success(create);
        if (!result.RootElement.GetProperty("acknowledged").GetBoolean()) throw Unavailable();
        return true;
    }, ct);

    public Task UpsertAsync(IReadOnlyList<PublicTicketDocument> documents, CancellationToken ct) => ExecuteAsync(async () =>
    {
        if (documents.Count == 0) return true;
        var data = new StringBuilder();
        foreach (var document in documents)
        {
            data.Append(JsonSerializer.Serialize(new { index = new { _id = document.Id } }, Json)).Append('\n');
            data.Append(JsonSerializer.Serialize(document, Json)).Append('\n');
        }
        using var response = await SendAsync(HttpMethod.Post, "/_bulk?refresh=wait_for", new StringContent(data.ToString(), Encoding.UTF8, "application/x-ndjson"), ct);
        Success(response);
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (result.RootElement.GetProperty("errors").GetBoolean()) throw Unavailable();
        return true;
    }, ct);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, settings.Endpoint.AbsoluteUri.TrimEnd('/') + "/" + settings.Index + path) { Content = content };
        return await http.SendAsync(request, ct);
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!settings.Enabled) throw new RuleException(ErrorKind.Unavailable, "Search is disabled. Ticket browsing remains available.");
        try { return await action(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        { throw Unavailable(); }
    }

    private static StringContent Body(object body) => new(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
    private static void Success(HttpResponseMessage response) { if (!response.IsSuccessStatusCode) throw Unavailable(); }
    private static RuleException Unavailable() => new(ErrorKind.Unavailable, "Search is temporarily unavailable. Ticket browsing remains available.");
}
