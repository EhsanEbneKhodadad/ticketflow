using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class AuthenticationWorkflowTests
{
    private const string Password = "TicketFlowDemo!2026";
    private static async Task<HttpResponseMessage> Post(HttpClient client, string url, object body)
    {
        using var csrf = JsonDocument.Parse(await client.GetStringAsync("/api/v1/auth/csrf"));
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Registration_ignores_privilege_input_uses_http_only_cookies_and_requires_csrf()
    {
        await using var db = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(db.ConnectionString);
        await app.InitializeAsync();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/register", new { email = "new@example.test", password = Password, name = "New Customer" })).StatusCode);
        var registered = await Post(client, "/api/v1/auth/register", new { email = "new@example.test", password = Password, name = "New Customer", role = "Admin" });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        using var user = JsonDocument.Parse(await registered.Content.ReadAsStringAsync());
        Assert.Equal("Customer", user.RootElement.GetProperty("role").GetString());
        var cookies = registered.Headers.GetValues("Set-Cookie").ToArray();
        Assert.Contains(cookies, cookie => cookie.StartsWith("ticketflow_access=", StringComparison.Ordinal) && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies, cookie => cookie.StartsWith("ticketflow_refresh=", StringComparison.Ordinal) && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("accessToken", await registered.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/agents")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, "/api/v1/auth/login", new { email = "new@example.test", password = "Incorrect!12345" })).StatusCode);
    }

    [Fact]
    public async Task Refresh_rotation_detects_reuse_and_revokes_the_active_session()
    {
        await using var db = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(db.ConnectionString);
        await app.InitializeAsync();
        using var client = app.CreateClient();
        var login = await Post(client, "/api/v1/auth/login", new { email = "customer@ticketflow.local", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var oldRefresh = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("ticketflow_refresh=", StringComparison.Ordinal)).Split(';')[0];
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/auth/refresh", new { })).StatusCode);
        using var replay = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var csrfResponse = await replay.GetAsync("/api/v1/auth/csrf");
        using var csrf = JsonDocument.Parse(await csrfResponse.Content.ReadAsStringAsync());
        var csrfCookie = csrfResponse.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("ticketflow_csrf=", StringComparison.Ordinal)).Split(';')[0];
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh") { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Cookie", oldRefresh + "; " + csrfCookie);
        request.Headers.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        Assert.Equal(HttpStatusCode.Unauthorized, (await replay.SendAsync(request)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_invalidates_a_previously_issued_access_cookie()
    {
        await using var db = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(db.ConnectionString);
        await app.InitializeAsync();
        using var client = app.CreateClient();
        var login = await Post(client, "/api/v1/auth/login", new { email = "customer@ticketflow.local", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var access = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("ticketflow_access=", StringComparison.Ordinal)).Split(';')[0];
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "/api/v1/auth/logout", new { })).StatusCode);
        using var stolen = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        stolen.DefaultRequestHeaders.Add("Cookie", access);
        Assert.Equal(HttpStatusCode.Unauthorized, (await stolen.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Password_reset_is_generic_single_use_and_revokes_sessions()
    {
        await using var db = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(db.ConnectionString);
        await app.InitializeAsync();
        using var client = app.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/auth/login", new { email = "customer@ticketflow.local", password = Password })).StatusCode);
        var known = await Post(client, "/api/v1/auth/forgot-password", new { email = "customer@ticketflow.local" });
        var unknown = await Post(client, "/api/v1/auth/forgot-password", new { email = "absent@example.test" });
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(await known.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        var token = app.Reset.Tokens["customer@ticketflow.local"];
        var reset = new { email = "customer@ticketflow.local", token, password = "ChangedPassword!2026" };
        Assert.Equal(HttpStatusCode.NoContent, (await Post(client, "/api/v1/auth/reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/api/v1/auth/reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/v1/auth/login", new { email = reset.email, password = reset.password })).StatusCode);
    }
}
