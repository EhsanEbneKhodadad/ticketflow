using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Persistence;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class SearchEndpointTests
{
    [Fact]
    public async Task Search_requires_authentication_and_reports_disabled_search_without_breaking_tickets()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/search/tickets?q=account")).StatusCode);
        await LoginAsync(client);
        using var response = await client.GetAsync("/api/v1/search/tickets?q=account");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(503, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Contains("disabled", problem.RootElement.GetProperty("detail").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/search/tickets?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task Enabled_endpoint_binds_filter_dates_and_enums_and_returns_only_safe_segments()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        var ids = new List<Guid>();
        DateOnly createdDay = default;
        await using var app = new WorkflowFactory(database.ConnectionString, searchIndex: new SearchStoreTests.CandidateIndex(ids));
        await app.InitializeAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "customer@ticketflow.local");
            var ticket = Ticket.Create(new Actor(user.Id, "Customer", "Customer"), "Public launch ticket",
                "Public <script>launch</script> description", Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941"), [], TicketPriority.High);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            ids.Add(ticket.Id);
            createdDay = DateOnly.FromDateTime(ticket.CreatedAt.UtcDateTime);
        }
        using var client = app.CreateClient();
        await LoginAsync(client);
        var today = createdDay.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var query in new[] { "q=launch", "q=launch&status=open", "q=launch&priority=high", "q=launch&sort=newest", $"q=launch&from={today}&to={today}" })
        {
            using var response = await client.GetAsync("/api/v1/search/tickets?" + query);
            Assert.True(response.StatusCode == HttpStatusCode.OK, query + ": " + await response.Content.ReadAsStringAsync());
        }
        var result = await client.GetFromJsonAsync<JsonElement>($"/api/v1/search/tickets?q=launch&status=open&priority=high&sort=newest&from={today}&to={today}");
        Assert.Equal(1, result.GetProperty("total").GetInt32());
        Assert.Equal(1000, result.GetProperty("candidateLimit").GetInt32());
        var item = result.GetProperty("items")[0];
        Assert.Equal(ids[0], item.GetProperty("ticket").GetProperty("id").GetGuid());
        Assert.Equal("high", item.GetProperty("ticket").GetProperty("priority").GetString());
        Assert.Contains(item.GetProperty("snippet").EnumerateArray(), segment => segment.GetProperty("highlighted").GetBoolean() && segment.GetProperty("text").GetString() == "launch");
        Assert.Contains("<script>", string.Concat(item.GetProperty("snippet").EnumerateArray().Select(segment => segment.GetProperty("text").GetString())));
        foreach (var query in new[] { "from=2026-10-29&to=2026-10-28", "sort=unexpected", "status=unknown", "priority=unknown", "from=no-date", "page=1001",
            "status=open&status=closed", "priority=high&Priority=low", "sort=newest&sort=relevance", "status=0", "q=" + new string('x', 161) })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/search/tickets?" + query)).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/v1/search/tickets?to=9999-12-31")).GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/API/V1/SEARCH/TICKETS/?STATUS=OPEN&priority=HIGH&sort=NEWEST")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tickets?status=open")).StatusCode);
        foreach (var query in new[] { "priority=high", "priority=urgent", "status=inProgress", "status=waitingForCustomer" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tickets?" + query)).StatusCode);
        foreach (var query in new[] { "status=unknown", "priority=unknown", "status=open&status=closed", "priority=high&priority=low" })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/tickets?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/API/V1/SEARCH/TICKETS/?status=open&status=closed")).StatusCode);
        using var schema = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var parameters = schema.RootElement.GetProperty("paths").GetProperty("/api/v1/search/tickets").GetProperty("get").GetProperty("parameters");
        foreach (var (name, expected) in new[] { ("Sort", "relevance"), ("Status", "open"), ("Priority", "high") })
        {
            var parameter = parameters.EnumerateArray().Single(p => p.GetProperty("name").GetString()!.Equals(name, StringComparison.OrdinalIgnoreCase));
            var type = parameter.GetProperty("schema");
            if (type.TryGetProperty("$ref", out var reference))
                type = schema.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1]);
            Assert.Contains(type.GetProperty("enum").EnumerateArray(), value => value.GetString() == expected);
        }
    }

    private static async Task LoginAsync(HttpClient client)
    {
        using var csrf = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/csrf"));
        using var login = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "customer@ticketflow.local", password = "TicketFlowDemo!2026" })
        };
        login.Headers.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        (await client.SendAsync(login)).EnsureSuccessStatusCode();
    }
}
