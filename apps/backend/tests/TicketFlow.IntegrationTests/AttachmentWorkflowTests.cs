using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class AttachmentWorkflowTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expected, $"Expected {expected}, got {response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, object input)
    {
        var csrf = await Json(await client.GetAsync("/api/v1/auth/csrf"));
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(input) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private static async Task Login(HttpClient client, string role) =>
        await Json(await Send(client, "/api/v1/auth/login", new { email = role + "@ticketflow.local", password = "TicketFlowDemo!2026" }));

    private static async Task<string> Create(HttpClient client)
    {
        var ticket = await Json(await Send(client, "/api/v1/tickets", new
        {
            title = "Attachment support issue",
            description = "Please see the attached diagnostic details.",
            categoryId = Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941"),
            tags = Array.Empty<string>()
        }), HttpStatusCode.Created);
        return "/api/v1/tickets/" + ticket.GetProperty("id").GetGuid();
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, string path, int version, string name = "details.txt", byte[]? bytes = null, Guid? messageId = null, bool csrf = true, string attachmentRoute = "attachments", string? expectedUser = null)
    {
        using var body = new MultipartFormDataContent();
        body.Add(new ByteArrayContent(bytes ?? Encoding.UTF8.GetBytes("Useful diagnostic details")), "file", name);
        body.Add(new StringContent(version.ToString(System.Globalization.CultureInfo.InvariantCulture)), "expectedVersion");
        if (messageId.HasValue) body.Add(new StringContent(messageId.Value.ToString()), "messageId");
        using var request = new HttpRequestMessage(HttpMethod.Post, path + "/" + attachmentRoute) { Content = body };
        // An early size rejection can precede body transmission; avoid racing the server's response with an 11 MiB socket write.
        request.Headers.ExpectContinue = true;
        if (expectedUser is not null) request.Headers.Add("X-TicketFlow-User", expectedUser);
        if (csrf)
        {
            var token = await Json(await client.GetAsync("/api/v1/auth/csrf"));
            request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
        }
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Initial_upload_rejects_changed_account_before_storage_or_ticket_mutation()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        app.UseKestrel();
        await app.InitializeAsync();
        using var owner = app.CreateClient();
        using var staff = app.CreateClient();
        await Login(owner, "customer");
        await Login(staff, "agent");
        var ownerId = (await Json(await owner.GetAsync("/api/v1/auth/me"))).GetProperty("id").GetGuid();
        var staffId = (await Json(await staff.GetAsync("/api/v1/auth/me"))).GetProperty("id").GetGuid();
        var path = await Create(owner);
        var before = await Json(await owner.GetAsync(path));

        // B owns this ticket and has valid CSRF: without the precondition this upload succeeds as B.
        var problem = await Json(await Upload(owner, path, 1, expectedUser: staffId.ToString()), HttpStatusCode.PreconditionFailed);
        using var withoutCsrf = await Upload(owner, path, 1, expectedUser: staffId.ToString(), csrf: false);
        Assert.Equal(HttpStatusCode.PreconditionFailed, withoutCsrf.StatusCode);
        Assert.Equal("application/problem+json", withoutCsrf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("account_changed", problem.GetProperty("code").GetString());
        Assert.DoesNotContain(ownerId.ToString(), problem.GetRawText());
        Assert.DoesNotContain(staffId.ToString(), problem.GetRawText());
        Assert.Empty(app.Storage.Objects);
        Assert.Equal(0, app.Storage.Deletes);
        Assert.Equal(before.GetRawText(), (await Json(await owner.GetAsync(path))).GetRawText());
        Assert.Empty((await Json(await owner.GetAsync(path + "/attachments"))).EnumerateArray());

        foreach (var route in new[] { path, path + "/attachments", "/api/v1/search/tickets?q=details", "/api/v1/categories" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, route);
            request.Headers.Add("X-TicketFlow-User", staffId.ToString());
            await Json(await owner.SendAsync(request), HttpStatusCode.PreconditionFailed);
        }
        await Json(await Upload(owner, path, 1, expectedUser: ownerId.ToString()), HttpStatusCode.Created);
        Assert.Single(app.Storage.Objects);
        Assert.Equal(2, (await Json(await owner.GetAsync(path))).GetProperty("version").GetInt32());
    }

    [Theory]
    [InlineData("absent", HttpStatusCode.OK)]
    [InlineData("correct", HttpStatusCode.OK)]
    [InlineData("malformed", HttpStatusCode.BadRequest)]
    [InlineData("duplicate", HttpStatusCode.BadRequest)]
    [InlineData("combined", HttpStatusCode.BadRequest)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    public async Task Account_precondition_preserves_authentication_and_validates_one_guid(string value, HttpStatusCode expected)
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        app.UseKestrel();
        await app.InitializeAsync();
        using var client = app.CreateClient();
        if (value != "anonymous") await Login(client, "customer");
        var id = value == "anonymous" ? Guid.NewGuid() : (await Json(await client.GetAsync("/api/v1/auth/me"))).GetProperty("id").GetGuid();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tickets");
        if (value == "duplicate") request.Headers.Add("X-TicketFlow-User", new[] { id.ToString(), id.ToString() });
        else if (value != "absent") request.Headers.Add("X-TicketFlow-User", value == "malformed" ? "invalid" : value == "combined" ? id + "," + id : id.ToString());
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Attachment_list_is_available_only_to_ticket_participants()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var owner = app.CreateClient();
        using var other = app.CreateClient();
        await Login(owner, "customer");
        await Json(await Send(other, "/api/v1/auth/register", new { email = "outsider@example.test", password = "TicketFlowDemo!2026", name = "Outsider" }), HttpStatusCode.Created);
        var path = await Create(owner);
        Assert.Equal(0, (await Json(await owner.GetAsync(path + "/attachments"))).GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path + "/attachments")).StatusCode);
    }

    [Fact]
    public async Task Upload_download_privacy_validation_and_concurrency_are_enforced()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        app.UseKestrel();
        await app.InitializeAsync();
        using var owner = app.CreateClient();
        using var agent = app.CreateClient();
        using var other = app.CreateClient();
        await Login(owner, "customer");
        await Login(agent, "agent");
        await Json(await Send(other, "/api/v1/auth/register", new { email = "other-upload@example.test", password = "TicketFlowDemo!2026", name = "Other" }), HttpStatusCode.Created);
        var path = await Create(owner);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, path, 1, bytes: new byte[70000], csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, path, 1, bytes: new byte[11 * 1024 * 1024], csrf: false)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, path, 1, "fake.png")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, path, 1, bytes: [])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, path, 1, bytes: new byte[10 * 1024 * 1024 + 1])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(other, path, 1)).StatusCode);
        var bytes = Encoding.UTF8.GetBytes(new string('x', 70000));
        var uploaded = await Json(await Upload(owner, path, 1, "../../private-details.txt", bytes), HttpStatusCode.Created);
        Assert.Equal("private-details.txt", uploaded.GetProperty("fileName").GetString());
        Assert.Equal(2, uploaded.GetProperty("ticketVersion").GetInt32());
        Assert.False(uploaded.GetProperty("internal").GetBoolean());
        Assert.DoesNotContain("objectKey", uploaded.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var downloadPath = path + "/attachments/" + uploaded.GetProperty("id").GetGuid() + "/download";
        using var download = await owner.GetAsync(downloadPath);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("nosniff", Assert.Single(download.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(downloadPath)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Upload(owner, path, 1)).StatusCode);
        var note = await Json(await Send(agent, path + "/messages", new { body = "Internal diagnostic note", @internal = true, expectedVersion = 2 }));
        var message = note.GetProperty("messages")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(owner, path, 3, messageId: message)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(agent, path, 3, messageId: Guid.NewGuid())).StatusCode);
        var internalUpload = await Json(await Upload(agent, path, 3, "internal-secret.txt", messageId: message), HttpStatusCode.Created);
        Assert.True(internalUpload.GetProperty("internal").GetBoolean());
        Assert.Equal("text/plain", internalUpload.GetProperty("contentType").GetString());
        Assert.Equal(1, (await Json(await owner.GetAsync(path + "/attachments"))).GetArrayLength());
        Assert.Equal(2, (await Json(await agent.GetAsync(path + "/attachments"))).GetArrayLength());
        Assert.DoesNotContain("internal-secret", (await Json(await owner.GetAsync(path))).GetRawText());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(path + "/attachments/" + internalUpload.GetProperty("id").GetGuid() + "/download")).StatusCode);
        var simultaneous = await Task.WhenAll(Upload(owner, path, 4), Upload(agent, path, 4));
        Assert.Single(simultaneous, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(simultaneous, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(5, (await Json(await owner.GetAsync(path))).GetProperty("version").GetInt32());
        for (var version = 5; version < 11; version++)
            await Json(await Upload(owner, path, version), HttpStatusCode.Created);
        var finalSlot = await Task.WhenAll(Upload(owner, path, 11), Upload(agent, path, 11));
        Assert.Single(finalSlot, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(finalSlot, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict, (await Upload(owner, path, 12)).StatusCode);
        Assert.Equal(10, app.Storage.Objects.Count);
        Assert.Equal(10, (await Json(await agent.GetAsync(path + "/attachments"))).GetArrayLength());
    }

    [Fact]
    public async Task Failed_postgres_commit_deletes_uploaded_object_and_rolls_back_metadata_and_audit()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var owner = app.CreateClient();
        await Login(owner, "customer");
        var path = await Create(owner);
        var id = Guid.Parse(path.Split('/').Last());
        app.Storage.BeforeUpload = async () =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
            await db.Tickets.Where(t => t.Id == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Version, t => t.Version + 1));
        };
        Assert.Equal(HttpStatusCode.Conflict, (await Upload(owner, path, 1)).StatusCode);
        Assert.Empty(app.Storage.Objects);
        Assert.Equal(1, app.Storage.Deletes);
        Assert.Equal(0, (await Json(await owner.GetAsync(path + "/attachments"))).GetArrayLength());
        Assert.DoesNotContain("attachmentAdded", (await Json(await owner.GetAsync(path))).GetRawText());
    }

    [Fact]
    public async Task Openapi_describes_typed_multipart_upload_and_attachment_response()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        using var client = app.CreateClient();
        var spec = await Json(await client.GetAsync("/openapi/v1.json"));
        var operation = spec.GetProperty("paths").GetProperty("/api/v1/tickets/{id}/attachments").GetProperty("post");
        var schemaNode = operation.GetProperty("requestBody").GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema");
        if (schemaNode.TryGetProperty("$ref", out var reference))
            schemaNode = spec.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/').Last());
        var schema = schemaNode.GetRawText();
        Assert.Contains("file", schema, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expectedVersion", schema, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("messageId", schema, StringComparison.OrdinalIgnoreCase);
        var response = operation.GetProperty("responses").GetProperty("201").GetRawText();
        Assert.Contains("AttachmentView", response);
    }

    [Fact]
    public async Task Storage_outage_returns_safe_503_and_preserves_ticket_metadata()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var owner = app.CreateClient();
        await Login(owner, "customer");
        var path = await Create(owner);
        app.Storage.BeforeUpload = () => throw new TicketFlow.Domain.Common.RuleException(TicketFlow.Domain.Common.ErrorKind.Unavailable, "Attachment storage is temporarily unavailable.");
        var problem = await Json(await Upload(owner, path, 1), HttpStatusCode.ServiceUnavailable);
        Assert.Equal(503, problem.GetProperty("status").GetInt32());
        Assert.DoesNotContain("ObjectKey", problem.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, (await Json(await owner.GetAsync(path))).GetProperty("version").GetInt32());
        Assert.Empty(app.Storage.Objects);
    }

    [Theory]
    [InlineData("attachments/")]
    [InlineData("ATTACHMENTS")]
    public async Task Upload_route_variants_apply_multipart_limits_before_parsing(string attachmentRoute)
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        app.UseKestrel();
        await app.InitializeAsync();
        using var owner = app.CreateClient();
        await Login(owner, "customer");
        var path = await Create(owner);
        var bytes = Encoding.UTF8.GetBytes(new string('x', 70 * 1024));
        for (var count = 1; count <= 2; count++)
        {
            var ticket = await Json(await owner.GetAsync(path));
            var version = ticket.GetProperty("version").GetInt32();
            var uploaded = await Json(await Upload(owner, path, version, bytes: bytes, attachmentRoute: attachmentRoute), HttpStatusCode.Created);
            Assert.Equal(version + 1, uploaded.GetProperty("ticketVersion").GetInt32());
            Assert.Equal(bytes.Length, uploaded.GetProperty("size").GetInt64());
            Assert.Equal(count, (await Json(await owner.GetAsync(path + "/attachments"))).GetArrayLength());
        }
    }
}
