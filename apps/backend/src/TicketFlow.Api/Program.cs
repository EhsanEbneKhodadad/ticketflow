using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using TicketFlow.Api.Errors;
using TicketFlow.Api.Features.Auth;
using TicketFlow.Api.Features.Administration;
using TicketFlow.Api.Features.Attachments;
using TicketFlow.Api.Features.Tickets;
using TicketFlow.Api.Features.Search;
using TicketFlow.Api.Security;
using TicketFlow.Infrastructure.Identity;
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
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase, allowIntegerValues: false)));
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 65536);
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();
builder.AddTicketFlowSecurity();
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

_ = app.Services.GetRequiredService<AuthSettings>();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.UseExpectedUserPrecondition();
app.UseRateLimiter();
app.UseAttachmentBodyLimit();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1")
        && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 400,
                Title = "Invalid request verification token.",
                Detail = "Refresh the page and try again."
            }, options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json");
            return;
        }
    }
    await next(context);
});
app.UseStatusCodePages();
app.UseTicketQueryNormalization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapSystemInfo();
app.MapAuth();
app.MapTickets();
app.MapSearch();
app.MapAttachments();
app.MapAdministration();
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing")) app.MapOpenApi();
await app.RunAsync();

public partial class Program;
