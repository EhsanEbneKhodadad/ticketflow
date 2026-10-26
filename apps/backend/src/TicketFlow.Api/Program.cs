using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Api.Features.System;
using TicketFlow.Api.Health;
using TicketFlow.Application.Abstractions;
using TicketFlow.Application.Bootstrap;
using TicketFlow.Infrastructure;
using TicketFlow.Infrastructure.Persistence;

var migrate = args.Contains("--migrate", StringComparer.Ordinal);
var seed = args.Contains("--seed", StringComparer.Ordinal);
var hostArgs = args.Where(arg => arg is not "--migrate" and not "--seed").ToArray();
var builder = WebApplication.CreateBuilder(hostArgs);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();
builder.Services.AddHealthChecks().AddCheck<DatabaseReadyHealthCheck>("database", tags: ["ready"]);

var bootstrapError = BootstrapPolicy.GetError(migrate, seed, builder.Environment.IsDevelopment(),
    builder.Configuration.GetValue<bool>("Seed:Enabled"));
if (bootstrapError is not null) throw new InvalidOperationException(bootstrapError);

var app = builder.Build();
if (migrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
    await db.Database.MigrateAsync();
    if (seed) await scope.ServiceProvider.GetRequiredService<IDevelopmentSeeder>().SeedAsync();
    app.Logger.LogInformation("Database bootstrap completed. Seed requested: {SeedRequested}", seed);
    await app.DisposeAsync();
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapSystemInfo();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi();
await app.RunAsync();

public partial class Program;
