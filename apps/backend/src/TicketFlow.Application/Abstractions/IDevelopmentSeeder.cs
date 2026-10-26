namespace TicketFlow.Application.Abstractions;

public interface IDevelopmentSeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
