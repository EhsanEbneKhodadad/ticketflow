using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Auth;
using TicketFlow.Application.Attachments;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.IntegrationTests;

public sealed class ResetCapture : IPasswordResetSender
{
    public Dictionary<string, string> Tokens { get; } = new();
    public Task SendAsync(string email, string token, CancellationToken ct)
    {
        Tokens[email] = token;
        return Task.CompletedTask;
    }
}

public sealed class WorkflowFactory(string connection) : WebApplicationFactory<Program>
{
    public ResetCapture Reset { get; } = new();
    public AttachmentStorageCapture Storage { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = connection,
            ["Seed:Enabled"] = "true",
            ["Seed:DemoPassword"] = "TicketFlowDemo!2026"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPasswordResetSender>();
            services.AddSingleton<IPasswordResetSender>(Reset);
            services.RemoveAll<IAttachmentStorage>();
            services.AddSingleton<IAttachmentStorage>(Storage);
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<IDevelopmentSeeder>().SeedAsync();
    }
}
