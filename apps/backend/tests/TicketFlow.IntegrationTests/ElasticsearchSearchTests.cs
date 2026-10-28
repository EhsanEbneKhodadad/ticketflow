using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Search;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Search;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Search")]
[Trait("Category", "Docker")]
public sealed class ElasticsearchSearchTests
{
    private static readonly Guid General = Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941");

    [Fact]
    public async Task Real_projection_supports_public_full_text_filters_updates_ownership_and_rebuilds()
    {
        await using var engine = await SearchEngine.CreateAsync();
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString, new Dictionary<string, string?>
        {
            ["Search:Enabled"] = "true",
            ["Search:Endpoint"] = engine.Endpoint,
            ["Search:Index"] = engine.Index
        });
        await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var users = await db.Users.ToArrayAsync();
        var customer = new Actor(users.Single(u => u.Email == "customer@ticketflow.local").Id, "Customer", "Customer");
        var agent = new Actor(users.Single(u => u.Email == "agent@ticketflow.local").Id, "Agent", "Agent");
        var first = Ticket.Create(customer, "Quantum account issue", "The satellite connection keeps dropping.", General, ["nebula"], TicketPriority.High);
        first.Reply(agent, "INTERNALquasar secret diagnosis", true, first.Version);
        first.Reply(agent, "Public orbital workaround <img src=x onerror=alert(1)>", false, first.Version);
        first.Attach(agent, "INTERNALfilename.txt", "text/plain", 12, "PRIVATEobjectkey", first.Messages[0].Id, first.Version);
        first.Assign(agent, agent.Id, agent.Name, first.Version);
        db.Tickets.Add(first);
        await db.SaveChangesAsync();
        var foreign = Ticket.Create(agent, "Foreign hidden account", "Foreign orbital customer description.", General, [], TicketPriority.Low);
        db.Tickets.Add(foreign);
        await db.SaveChangesAsync();
        var projection = scope.ServiceProvider.GetRequiredService<SearchProjectionWorkflow>();
        var search = scope.ServiceProvider.GetRequiredService<TicketSearchWorkflow>();
        await projection.ReconcileAsync(default);
        await projection.ReconcileAsync(default);
        var replyMatch = await search.SearchAsync(customer, new(Q: "orbital"), default);
        Assert.Equal(1, replyMatch.Total);
        Assert.Equal(first.Id, Assert.Single(replyMatch.Items).Ticket.Id);
        Assert.Contains(replyMatch.Items[0].Snippet, segment => segment.Highlighted && segment.Text == "orbital");
        Assert.Contains("<img", string.Concat(replyMatch.Items[0].Snippet.Select(segment => segment.Text)));
        foreach (var text in new[] { "quantum", "satellite", "nebula", "TKT-1001", "1001" })
            Assert.Equal(first.Id, Assert.Single((await search.SearchAsync(customer, new(Q: text), default)).Items).Ticket.Id);
        foreach (var text in new[] { "INTERNALquasar", "INTERNALfilename", "PRIVATEobjectkey" })
            Assert.Empty((await search.SearchAsync(agent, new(Q: text), default)).Items);
        var rawIndex = await engine.Http.GetStringAsync(engine.Index + "/_search?size=100");
        Assert.DoesNotContain("INTERNAL", rawIndex);
        Assert.DoesNotContain("PRIVATEobjectkey", rawIndex);
        var today = DateOnly.FromDateTime(first.CreatedAt.UtcDateTime);
        var filters = new TicketSearchQuery(Q: "orbital", Status: TicketStatus.Open, Priority: TicketPriority.High,
            CategoryId: General, AgentId: agent.Id, From: today, To: today);
        Assert.Equal(1, (await search.SearchAsync(customer, filters, default)).Total);
        Assert.Equal(0, (await search.SearchAsync(customer, filters with { Status = TicketStatus.Closed }, default)).Total);
        Assert.Equal(2, (await search.SearchAsync(agent, new(Q: "orbital", Sort: SearchSort.Newest), default)).Total);
        Assert.Equal(foreign.Id, (await search.SearchAsync(agent, new(Sort: SearchSort.Newest), default)).Items[0].Ticket.Id);
        using var client = app.CreateClient();
        using var csrf = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/csrf"));
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "customer@ticketflow.local", password = "TicketFlowDemo!2026" })
        };
        login.Headers.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        (await client.SendAsync(login)).EnsureSuccessStatusCode();
        var contract = await client.GetFromJsonAsync<JsonElement>($"/api/v1/search/tickets?q=orbital&status=open&priority=high&categoryId={General}&agentId={agent.Id}&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&sort=relevance");
        Assert.Equal(1, contract.GetProperty("total").GetInt32());
        Assert.Equal(1000, contract.GetProperty("candidateLimit").GetInt32());
        Assert.Equal("high", contract.GetProperty("items")[0].GetProperty("ticket").GetProperty("priority").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/search/tickets?from=2026-10-29&to=2026-10-28")).StatusCode);
        first.Edit(customer, "Neutrino account issue", first.Description, General, first.Tags, first.Version);
        await db.SaveChangesAsync();
        await projection.ReconcileAsync(default);
        Assert.Equal(0, (await search.SearchAsync(customer, new(Q: "quantum"), default)).Total);
        Assert.Equal(1, (await search.SearchAsync(customer, new(Q: "neutrino"), default)).Total);
        // A deletion after EnsureIndex must not let bulk auto-create a dynamic GUID/text mapping.
        var index = scope.ServiceProvider.GetRequiredService<ITicketSearchIndex>();
        var source = scope.ServiceProvider.GetRequiredService<ISearchTicketStore>();
        await index.EnsureIndexAsync(default);
        (await engine.Http.DeleteAsync(engine.Index)).EnsureSuccessStatusCode();
        await index.UpsertAsync(await source.ReadBatchAsync(0, await source.LastNumberAsync(default), 100, default), default);
        using (var mapping = JsonDocument.Parse(await engine.Http.GetStringAsync(engine.Index + "/_mapping")))
        {
            var definition = mapping.RootElement.GetProperty(engine.Index).GetProperty("mappings");
            Assert.Equal("strict", definition.GetProperty("dynamic").GetString());
            var fields = definition.GetProperty("properties");
            Assert.Equal("keyword", fields.GetProperty("requesterId").GetProperty("type").GetString());
            Assert.Equal("keyword", fields.GetProperty("status").GetProperty("type").GetString());
            Assert.Equal("keyword", fields.GetProperty("agentId").GetProperty("type").GetString());
            Assert.Equal("date", fields.GetProperty("createdAt").GetProperty("type").GetString());
        }
        await projection.ReconcileAsync(default);
        Assert.Equal(first.Id, Assert.Single((await search.SearchAsync(customer, new(Q: "orbital"), default)).Items).Ticket.Id);
        await db.Tickets.Where(t => t.Id == first.Id).ExecuteUpdateAsync(updates => updates.SetProperty(t => t.RequesterId, agent.Id));
        Assert.Equal(0, (await search.SearchAsync(customer, new(Q: "orbital"), default)).Total);
        Assert.Equal(0, (await search.SearchAsync(customer, new(Q: "orbital", Page: 2, PageSize: 1), default)).Total);
        (await engine.Http.DeleteAsync(engine.Index)).EnsureSuccessStatusCode();
        Assert.Equal(ErrorKind.Unavailable, (await Assert.ThrowsAsync<RuleException>(() => search.SearchAsync(customer, new(), default))).Kind);
        await projection.ReconcileAsync(default);
        Assert.Equal(2, (await search.SearchAsync(agent, new(Q: "orbital"), default)).Total);
    }

    [Fact]
    public async Task Real_elasticsearch_limits_candidate_window_and_filters_customer_scope()
    {
        await using var engine = await SearchEngine.CreateAsync();
        var index = new ElasticsearchTicketIndex(engine.Http, new(true, new Uri(engine.Endpoint), engine.Index, 15));
        await index.EnsureIndexAsync(default);
        var customer = new Actor(Guid.NewGuid(), "Customer", "Customer");
        var stranger = Guid.NewGuid();
        var created = new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
        var documents = Enumerable.Range(1, 1105).Select(n => new PublicTicketDocument(Guid.NewGuid(), n, "TKT-" + n,
            "Shared rocket title", "Public description", [], [], n <= 1005 ? customer.Id : stranger, created.AddSeconds(n),
            n == 1 ? TicketStatus.Open : TicketStatus.Closed, n == 1 ? TicketPriority.High : TicketPriority.Low,
            n == 1 ? General : Guid.Empty, n == 1 ? customer.Id : null)).ToArray();
        foreach (var batch in documents.Chunk(100)) await index.UpsertAsync(batch, default);
        var candidates = await index.SearchAsync(customer, new(Q: "rocket", Sort: SearchSort.Newest), default);
        Assert.Equal(1000, candidates.Count);
        Assert.Equal(documents[1004].Id, candidates[0]);
        Assert.DoesNotContain(candidates, id => documents.Skip(1005).Any(document => document.Id == id));
        Assert.Equal(1000, (await index.SearchAsync(customer with { Role = "Agent" }, new(), default)).Count);
        // The oldest open ticket must survive even behind >1000 newer closed matches.
        var filtered = await index.SearchAsync(customer, new(Q: "rocket", Sort: SearchSort.Newest,
            Status: TicketStatus.Open, Priority: TicketPriority.High, CategoryId: General, AgentId: customer.Id), default);
        Assert.Equal(documents[0].Id, Assert.Single(filtered));
        var day = DateOnly.FromDateTime(documents[0].CreatedAt.UtcDateTime);
        Assert.Equal(documents[0].Id, Assert.Single(await index.SearchAsync(customer,
            new(Q: "rocket", Status: TicketStatus.Open, From: day, To: day), default)));
        Assert.Empty(await index.SearchAsync(customer, new(Q: "rocket", From: day.AddDays(1)), default));
        Assert.Equal(1000, (await index.SearchAsync(customer, new(To: DateOnly.MaxValue), default)).Count);
    }

    private sealed class SearchEngine : IAsyncDisposable
    {
        private IContainer? _container;
        public string Endpoint { get; private set; } = "";
        public string Index { get; } = "ticketflow-test-" + Guid.NewGuid().ToString("N");
        public HttpClient Http { get; private set; } = null!;

        public static async Task<SearchEngine> CreateAsync()
        {
            var engine = new SearchEngine();
            var endpoint = Environment.GetEnvironmentVariable("TICKETFLOW_TEST_ELASTICSEARCH");
            try
            {
                if (string.IsNullOrWhiteSpace(endpoint))
                {
                    engine._container = new ContainerBuilder("docker.elastic.co/elasticsearch/elasticsearch:8.19.4")
                        .WithPortBinding(9200, true).WithEnvironment("discovery.type", "single-node")
                        .WithEnvironment("xpack.security.enabled", "false").WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
                        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(9200).ForPath("/"))).Build();
                    await engine._container.StartAsync();
                    endpoint = $"http://{engine._container.Hostname}:{engine._container.GetMappedPublicPort(9200)}";
                }
                engine.Endpoint = endpoint;
                engine.Http = new HttpClient { BaseAddress = new Uri(endpoint.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
                return engine;
            }
            catch
            {
                if (engine._container is not null) await engine._container.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                // Clean only this test's exact random index and its deterministic, equally scoped template.
                using var response = await Http.DeleteAsync(Index);
                using var template = await Http.DeleteAsync("_index_template/ticketflow-search-" + Index);
            }
            finally
            {
                Http.Dispose();
                if (_container is not null) await _container.DisposeAsync();
            }
        }
    }
}
