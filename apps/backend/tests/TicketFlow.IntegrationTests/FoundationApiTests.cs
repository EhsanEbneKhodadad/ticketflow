using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Host")]
public sealed class FoundationApiTests
{
    private sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
            .UseEnvironment("Testing")
            .ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1;Command Timeout=1;Pooling=false"
            }));
    }

    [Fact]
    public async Task Liveness_does_not_require_a_database()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_fails_when_postgres_is_unavailable()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task System_info_has_a_versioned_documented_contract()
    {
        await using var factory = new Factory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/system/info");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("TicketFlow", body.RootElement.GetProperty("name").GetString());
        using var spec = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        Assert.True(spec.RootElement.GetProperty("paths").TryGetProperty("/api/v1/system/info", out _));
    }
}
