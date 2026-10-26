using TicketFlow.Infrastructure;
using TicketFlow.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddInfrastructure();
builder.Services.AddHostedService<WorkerLifecycle>();
await builder.Build().RunAsync();
