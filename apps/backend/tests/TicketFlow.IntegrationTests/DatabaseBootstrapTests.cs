using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using TicketFlow.Infrastructure.Persistence;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class DatabaseBootstrapTests
{
    [Fact]
    public async Task Migrations_and_repeated_seeding_create_exactly_two_categories()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17.6-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<TicketFlowDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new TicketFlowDbContext(options);
        await db.Database.MigrateAsync();
        var seeder = new DevelopmentSeeder(db);
        await seeder.SeedAsync();
        await seeder.SeedAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(new[] { "billing", "general" }, await db.Categories.OrderBy(c => c.Slug).Select(c => c.Slug).ToArrayAsync());
    }
}
