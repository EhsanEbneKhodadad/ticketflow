using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Persistence;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class OutboxPersistenceTests
{
    [Fact]
    public async Task Ticket_and_outbox_are_committed_together_and_rollback_together()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        var options = new DbContextOptionsBuilder<TicketFlowDbContext>().UseNpgsql(database.ConnectionString).Options;
        var actor = new Actor(Guid.NewGuid(), "Unknown", "Customer");
        await using (var db = new TicketFlowDbContext(options))
        {
            db.Tickets.Add(Ticket.Create(actor, "Valid title", "Valid description", Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941"), [], TicketPriority.Low));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using (var db = new TicketFlowDbContext(options))
        {
            Assert.Empty(await db.Tickets.ToListAsync());
            Assert.Empty(await db.OutboxMessages.ToListAsync());
            var customer = await db.Users.SingleAsync(u => u.Email == "customer@ticketflow.local");
            var ticket = Ticket.Create(new(customer.Id, customer.DisplayName, "Customer"), "Valid title", "Valid description", Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941"), [], TicketPriority.Low);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
        }
        await using (var db = new TicketFlowDbContext(options))
        {
            var stored = Assert.Single(await db.Tickets.ToListAsync());
            var message = Assert.Single(await db.OutboxMessages.ToListAsync());
            Assert.Equal(stored.Id, message.TicketId);
            Assert.Equal("created", message.Kind);
        }
    }
}
