using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Abstractions;
using TicketFlow.Infrastructure.Identity;
using TicketFlow.Infrastructure.Persistence;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class SessionMigrationTests
{
    [Fact]
    public async Task Existing_refresh_sessions_upgrade_safely_and_are_explicitly_revoked()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        await db.GetService<IMigrator>().MigrateAsync("20261027051040_IdentityAndTicketWorkflow");
        await scope.ServiceProvider.GetRequiredService<IDevelopmentSeeder>().SeedAsync();
        var user = await db.Users.FirstAsync();
        var family = Guid.NewGuid();
        db.RefreshSessions.Add(new RefreshSession { Id = Guid.NewGuid(), UserId = user.Id, FamilyId = family, TokenHash = new string('A', 64), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
        await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        var upgraded = await db.SessionFamilies.SingleAsync(f => f.Id == family);
        Assert.NotNull(upgraded.RevokedAt);
        Assert.Equal(user.Id, upgraded.UserId);
        Assert.Equal(1, await db.RefreshSessions.CountAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
