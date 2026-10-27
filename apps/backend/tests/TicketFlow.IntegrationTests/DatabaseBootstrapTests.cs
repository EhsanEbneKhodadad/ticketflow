using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Abstractions;
using TicketFlow.Infrastructure.Persistence;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class DatabaseBootstrapTests
{
    [Fact]
    public async Task Migrations_and_repeated_seeding_create_exactly_two_categories()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        await scope.ServiceProvider.GetRequiredService<IDevelopmentSeeder>().SeedAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(new[] { "billing", "general" }, await db.Categories.OrderBy(c => c.Slug).Select(c => c.Slug).ToArrayAsync());
        Assert.Equal(3, await db.Users.CountAsync());
    }
}
