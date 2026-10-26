using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Abstractions;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<TicketFlowDbContext>((provider, options) =>
        {
            var connection = provider.GetRequiredService<IConfiguration>().GetConnectionString("Database");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Configure ConnectionStrings:Database before using TicketFlow persistence.");
            options.UseNpgsql(connection);
        });
        services.AddScoped<IDevelopmentSeeder, DevelopmentSeeder>();
        return services;
    }
}
