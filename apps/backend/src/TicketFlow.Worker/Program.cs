using TicketFlow.Infrastructure;
using TicketFlow.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddInfrastructure();
builder.Services.AddHostedService<WorkerLifecycle>();
builder.Services.AddHostedService<SearchProjectionService>();
await builder.Build().RunAsync();
