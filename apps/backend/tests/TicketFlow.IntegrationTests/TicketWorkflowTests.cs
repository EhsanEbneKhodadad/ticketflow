using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class TicketWorkflowTests
{
    private const string Password = "TicketFlowDemo!2026";
    private static readonly Guid General = Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941");

    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object input)
    {
        using var csrf = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/csrf"));
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(input) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {body}");
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, string role) =>
        Send(client, HttpMethod.Post, "/api/v1/auth/login", new { email = role + "@ticketflow.local", password = Password });

    private static object NewTicket(string title = "Cannot access account") => new
    {
        title,
        description = "My account rejects my password after signing out.",
        categoryId = General,
        tags = new[] { "login" },
        priority = "high"
    };

    [Fact]
    public async Task Customer_agent_journey_enforces_ownership_internal_privacy_and_version_checks()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var customer = app.CreateClient();
        using var agent = app.CreateClient();
        using var other = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await customer.GetAsync("/api/v1/tickets")).StatusCode);
        await Json(await Login(customer, "customer"));
        var agentUser = await Json(await Login(agent, "agent"));
        await Json(await Send(other, HttpMethod.Post, "/api/v1/auth/register", new { email = "other@example.test", password = Password, name = "Other Customer" }), HttpStatusCode.Created);
        var ticket = await Json(await Send(customer, HttpMethod.Post, "/api/v1/tickets", NewTicket()), HttpStatusCode.Created);
        var id = ticket.GetProperty("id").GetGuid();
        var path = "/api/v1/tickets/" + id;
        Assert.Equal("TKT-1001", ticket.GetProperty("number").GetString());
        Assert.Equal(1, ticket.GetProperty("version").GetInt32());
        var page = await Json(await other.GetAsync("/api/v1/tickets?q=account"));
        Assert.Equal(0, page.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(other, HttpMethod.Patch, path + "/status", new { status = "resolved", expectedVersion = 1 })).StatusCode);
        Assert.Equal(1, (await Json(await agent.GetAsync("/api/v1/tickets?unassigned=true"))).GetProperty("total").GetInt32());

        await Json(await Send(agent, HttpMethod.Patch, path + "/assignment", new { agentId = agentUser.GetProperty("id").GetGuid(), expectedVersion = 1 }));
        await Json(await Send(agent, HttpMethod.Patch, path + "/status", new { status = "inProgress", expectedVersion = 2 }));
        await Json(await Send(agent, HttpMethod.Post, path + "/messages", new { body = "Escalation-only diagnosis", @internal = true, expectedVersion = 3 }));
        await Json(await Send(agent, HttpMethod.Post, path + "/messages", new { body = "Please try signing in again.", @internal = false, expectedVersion = 4 }));
        var publicView = await Json(await customer.GetAsync(path));
        Assert.DoesNotContain("Escalation-only diagnosis", publicView.GetRawText());
        Assert.DoesNotContain("internalNoteAdded", publicView.GetRawText());
        Assert.Single(publicView.GetProperty("messages").EnumerateArray());
        Assert.Equal(2, (await Json(await agent.GetAsync(path))).GetProperty("messages").GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(customer, HttpMethod.Post, path + "/messages", new { body = "Private", @internal = true, expectedVersion = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(customer, HttpMethod.Patch, path + "/priority", new { priority = "urgent", expectedVersion = 5 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(agent, HttpMethod.Patch, path + "/priority", new { priority = "low", expectedVersion = 1 })).StatusCode);

        await Json(await Send(customer, HttpMethod.Put, path, new { title = "Updated account issue", description = "The issue continues on a second browser too.", categoryId = General, tags = new[] { "login" }, expectedVersion = 5 }));
        await Json(await Send(agent, HttpMethod.Patch, path + "/status", new { status = "resolved", expectedVersion = 6 }));
        await Json(await Send(customer, HttpMethod.Patch, path + "/status", new { status = "closed", expectedVersion = 7 }));
        Assert.Equal(HttpStatusCode.Conflict, (await Send(customer, HttpMethod.Post, path + "/messages", new { body = "Cannot reply to closed ticket.", @internal = false, expectedVersion = 8 })).StatusCode);
        await Json(await Send(customer, HttpMethod.Patch, path + "/status", new { status = "open", expectedVersion = 8 }));
        var final = await Json(await Send(customer, HttpMethod.Post, path + "/messages", new { body = "Thanks, reopening with more details.", @internal = false, expectedVersion = 9 }));
        Assert.Equal(10, final.GetProperty("version").GetInt32());
        Assert.Equal("open", final.GetProperty("status").GetString());
        Assert.Contains(final.GetProperty("audit").EnumerateArray(), item => item.GetProperty("kind").GetString() == "statusChanged");
    }

    [Fact]
    public async Task Invalid_inputs_and_missing_csrf_do_not_create_tickets()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var customer = app.CreateClient();
        await Json(await Login(customer, "customer"));
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.PostAsJsonAsync("/api/v1/tickets", NewTicket())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(customer, HttpMethod.Post, "/api/v1/tickets", NewTicket("x"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.GetAsync("/api/v1/tickets?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await customer.GetAsync("/api/v1/tickets?page=10001")).StatusCode);
        Assert.Equal(0, (await Json(await customer.GetAsync("/api/v1/tickets"))).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Simultaneous_updates_allow_one_winner_and_preserve_the_other_as_a_conflict()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var customer = app.CreateClient();
        using var one = app.CreateClient();
        using var two = app.CreateClient();
        await Json(await Login(customer, "customer"));
        await Json(await Login(one, "agent"));
        await Json(await Login(two, "agent"));
        var created = await Json(await Send(customer, HttpMethod.Post, "/api/v1/tickets", NewTicket()), HttpStatusCode.Created);
        var path = "/api/v1/tickets/" + created.GetProperty("id").GetGuid();
        var results = await Task.WhenAll(
            Send(one, HttpMethod.Patch, path + "/priority", new { priority = "urgent", expectedVersion = 1 }),
            Send(two, HttpMethod.Patch, path + "/priority", new { priority = "low", expectedVersion = 1 }));
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(2, (await Json(await customer.GetAsync(path))).GetProperty("version").GetInt32());
    }
}
