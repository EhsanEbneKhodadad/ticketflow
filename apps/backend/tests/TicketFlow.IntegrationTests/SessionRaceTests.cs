using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class SessionRaceTests
{
    [Fact]
    public async Task Logout_cannot_miss_a_refresh_that_was_already_waiting_for_its_row_lock()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var initial = app.CreateClient();
        using var csrf = JsonDocument.Parse(await initial.GetStringAsync("/api/v1/auth/csrf"));
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "customer@ticketflow.local", password = "TicketFlowDemo!2026" })
        };
        loginRequest.Headers.Add("X-CSRF-TOKEN", csrf.RootElement.GetProperty("token").GetString());
        var login = await initial.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var pairs = login.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]).ToArray();
        var access = pairs.Single(c => c.StartsWith("ticketflow_access=", StringComparison.Ordinal));
        var refresh = pairs.Single(c => c.StartsWith("ticketflow_refresh=", StringComparison.Ordinal));
        var raw = refresh["ticketflow_refresh=".Length..];
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        var cookies = access + "; " + refresh;
        using var rotating = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        using var loggingOut = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        await using var blocker = new NpgsqlConnection(database.ConnectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var command = new NpgsqlCommand("SELECT 1 FROM refresh_sessions WHERE token_hash=@hash FOR UPDATE", blocker, transaction))
        {
            command.Parameters.AddWithValue("hash", hash);
            await command.ExecuteScalarAsync();
        }
        var rotation = Post(rotating, "/api/v1/auth/refresh", cookies);
        await Waiting(database.ConnectionString, 1);
        var logout = Post(loggingOut, "/api/v1/auth/logout", cookies);
        await Waiting(database.ConnectionString, 2);
        await transaction.CommitAsync();
        await rotation;
        Assert.Equal(HttpStatusCode.NoContent, (await logout).StatusCode);
        using var after = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        after.DefaultRequestHeaders.Add("Cookie", cookies);
        Assert.Equal(HttpStatusCode.Unauthorized, (await after.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, string cookies)
    {
        using var get = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        get.Headers.Add("Cookie", cookies);
        var response = await client.SendAsync(get);
        using var token = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var csrfCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("ticketflow_csrf=", StringComparison.Ordinal)).Split(';')[0];
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Cookie", cookies + "; " + csrfCookie);
        request.Headers.Add("X-CSRF-TOKEN", token.RootElement.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private static async Task Waiting(string connection, int count)
    {
        await using var observer = new NpgsqlConnection(connection);
        await observer.OpenAsync();
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND query ILIKE '%refresh_sessions%'", observer);
            if (Convert.ToInt32(await command.ExecuteScalarAsync()) >= count) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Expected database row-lock wait was not observed.");
    }
}
