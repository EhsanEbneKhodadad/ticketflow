using Hangfire;
using Hangfire.PostgreSql;
using TicketFlow.Infrastructure;
using TicketFlow.Infrastructure.Notifications;
using TicketFlow.Worker;

var prepareJobs = args.Contains("--prepare-jobs", StringComparer.Ordinal);
var replayIndex = Array.IndexOf(args, "--replay");
if (replayIndex >= 0 && (replayIndex + 1 >= args.Length || !Guid.TryParse(args[replayIndex + 1], out _)))
    throw new ArgumentException("Use --replay followed by a failed outbox event GUID.");
var hostArgs = args.Where((arg, index) => arg != "--prepare-jobs" && arg != "--replay" && (replayIndex < 0 || index != replayIndex + 1)).ToArray();
var builder = Host.CreateApplicationBuilder(hostArgs);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddInfrastructure();
var connection = builder.Configuration.GetConnectionString("Database");
if (prepareJobs)
{
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configure ConnectionStrings:Database.");
    GlobalConfiguration.Configuration.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connection),
        new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = true });
    return;
}
if (replayIndex >= 0)
{
    using var host = builder.Build();
    using var scope = host.Services.CreateScope();
    var replayed = await scope.ServiceProvider.GetRequiredService<OutboxProcessor>().ReplayAsync(Guid.Parse(args[replayIndex + 1]), CancellationToken.None);
    if (!replayed) throw new InvalidOperationException("No failed event with that ID exists.");
    return;
}
builder.Services.AddHostedService<WorkerLifecycle>();
builder.Services.AddHostedService<SearchProjectionService>();
if (builder.Configuration.GetValue<bool>("Jobs:Enabled"))
{
    if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Configure ConnectionStrings:Database.");
    builder.Services.AddHangfire(configuration => configuration.UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connection), new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = false }));
    builder.Services.AddHangfireServer(options => options.WorkerCount = 1);
    builder.Services.AddScoped<OutboxJobs>();
    builder.Services.AddHostedService<OutboxSchedule>();
}
await builder.Build().RunAsync();
