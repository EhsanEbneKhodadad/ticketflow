using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class AdministrationHttpTests
{
    private static async Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object input)
    {
        using var csrf = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/csrf"));
        using var req = new HttpRequestMessage(method, path) { Content = JsonContent.Create(input) };
        req.Headers.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        return await client.SendAsync(req);
    }
    private static async Task<JsonElement> Body(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {text}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }
    private static Task<HttpResponseMessage> Login(HttpClient client, string role) => Send(client, HttpMethod.Post, "/api/v1/auth/login", new { email = role + "@ticketflow.local", password = "TicketFlowDemo!2026" });

    [Fact]
    public async Task Dashboard_scopes_customers_and_administration_requires_admin()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var customer = app.CreateClient();
        using var other = app.CreateClient();
        using var agent = app.CreateClient();
        using var admin = app.CreateClient();
        await Body(await Login(customer, "customer"));
        var agentUser = await Body(await Login(agent, "agent"));
        await Body(await Login(admin, "admin"));
        await Body(await Send(other, HttpMethod.Post, "/api/v1/auth/register", new { email = "other@example.test", name = "Other", password = "TicketFlowDemo!2026" }), HttpStatusCode.Created);
        await Body(await Send(customer, HttpMethod.Post, "/api/v1/tickets", new { title = "Cannot sign in", description = "Unable to access my account right now", categoryId = Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941"), tags = Array.Empty<string>(), priority = "high" }), HttpStatusCode.Created);
        Assert.Equal(1, (await Body(await customer.GetAsync("/api/v1/dashboard"))).GetProperty("total").GetInt32());
        Assert.Equal(0, (await Body(await other.GetAsync("/api/v1/dashboard"))).GetProperty("total").GetInt32());
        var dashboard = await Body(await agent.GetAsync("/api/v1/dashboard"));
        Assert.Equal(1, dashboard.GetProperty("total").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("highPriority").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await agent.GetAsync("/api/v1/admin/categories")).StatusCode);
        var category = await Body(await Send(admin, HttpMethod.Post, "/api/v1/admin/categories", new { name = "Technical", slug = "technical" }), HttpStatusCode.Created);
        Assert.Equal("technical", category.GetProperty("slug").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, HttpMethod.Post, "/api/v1/admin/categories", new { name = "Other", slug = "technical" })).StatusCode);
        var id = agentUser.GetProperty("id").GetGuid();
        await Body(await Send(admin, HttpMethod.Patch, $"/api/v1/admin/users/{id}/role", new { role = "Customer" }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await agent.GetAsync("/api/v1/dashboard")).StatusCode);
    }
}
