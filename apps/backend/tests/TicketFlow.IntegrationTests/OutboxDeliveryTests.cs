using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Notifications;
using TicketFlow.Infrastructure.Persistence;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class OutboxDeliveryTests
{
    [Fact]
    public async Task Concurrent_processors_claim_a_message_only_once_and_failed_work_can_be_replayed()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        var options = new DbContextOptionsBuilder<TicketFlowDbContext>().UseNpgsql(database.ConnectionString).Options;
        Guid id;
        await using (var db = new TicketFlowDbContext(options))
        {
            var requester = await db.Users.SingleAsync(u => u.Email == "customer@ticketflow.local");
            var agent = await db.Users.SingleAsync(u => u.Email == "agent@ticketflow.local");
            var ticket = Ticket.Create(new(requester.Id, requester.DisplayName, "Customer"), "Valid title", "Valid description", Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941"), [], TicketPriority.Low);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            ticket.Assign(new(agent.Id, agent.DisplayName, "Agent"), agent.Id, agent.DisplayName, ticket.Version);
            ticket.Reply(new(requester.Id, requester.DisplayName, "Customer"), "Hello", false, ticket.Version);
            await db.SaveChangesAsync();
            id = (await db.OutboxMessages.SingleAsync(m => m.Kind == "replyAdded")).Id;
            await db.OutboxMessages.Where(m => m.Id != id).ExecuteUpdateAsync(s => s.SetProperty(m => m.CompletedAt, DateTimeOffset.UtcNow));
        }
        var sender = new CaptureSender();
        await using (var a = new TicketFlowDbContext(options))
        await using (var b = new TicketFlowDbContext(options))
        {
            var first = new OutboxProcessor(a, sender, NullLogger<OutboxProcessor>.Instance);
            var second = new OutboxProcessor(b, sender, NullLogger<OutboxProcessor>.Instance);
            await Task.WhenAll(first.DispatchOneAsync(default), second.DispatchOneAsync(default));
        }
        Assert.Single(sender.Recipients);
        Assert.Equal("agent@ticketflow.local", sender.Recipients[0]);
        await using (var db = new TicketFlowDbContext(options))
        {
            var message = await db.OutboxMessages.SingleAsync(m => m.Id == id);
            Assert.NotNull(message.CompletedAt);
            message.CompletedAt = null;
            message.FailedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            var processor = new OutboxProcessor(db, sender, NullLogger<OutboxProcessor>.Instance);
            Assert.True(await processor.ReplayAsync(id, default));
        }
        await using (var db = new TicketFlowDbContext(options))
        {
            await new OutboxProcessor(db, sender, NullLogger<OutboxProcessor>.Instance).DispatchOneAsync(default);
            Assert.Equal(2, sender.Recipients.Count);
        }
    }

    [Fact]
    public async Task Expired_lease_recovers_and_repeated_failures_require_replay()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        var options = new DbContextOptionsBuilder<TicketFlowDbContext>().UseNpgsql(database.ConnectionString).Options;
        Guid id;
        await using (var db = new TicketFlowDbContext(options))
        {
            var customer = await db.Users.SingleAsync(u => u.Email == "customer@ticketflow.local");
            var agent = await db.Users.SingleAsync(u => u.Email == "agent@ticketflow.local");
            var ticket = Ticket.Create(new(customer.Id, customer.DisplayName, "Customer"), "Valid title", "Valid description", Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941"), [], TicketPriority.Low);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            ticket.Assign(new(agent.Id, agent.DisplayName, "Agent"), agent.Id, agent.DisplayName, ticket.Version);
            ticket.Reply(new(customer.Id, customer.DisplayName, "Customer"), "Hello", false, ticket.Version);
            await db.SaveChangesAsync();
            id = (await db.OutboxMessages.SingleAsync(m => m.Kind == "replyAdded")).Id;
            await db.OutboxMessages.Where(m => m.Id != id).ExecuteUpdateAsync(s => s.SetProperty(m => m.CompletedAt, DateTimeOffset.UtcNow));
            await db.OutboxMessages.Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.LeaseId, Guid.NewGuid()).SetProperty(m => m.LeaseUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }
        var sender = new CaptureSender { Fail = true };
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var db = new TicketFlowDbContext(options);
            await new OutboxProcessor(db, sender, NullLogger<OutboxProcessor>.Instance).DispatchOneAsync(default);
            await db.OutboxMessages.Where(m => m.Id == id && m.FailedAt == null).ExecuteUpdateAsync(s => s.SetProperty(m => m.NextAttemptAt, (DateTimeOffset?)null));
        }
        await using (var db = new TicketFlowDbContext(options))
        {
            var item = await db.OutboxMessages.SingleAsync(m => m.Id == id);
            Assert.Equal(5, item.Attempts);
            Assert.NotNull(item.FailedAt);
            var processor = new OutboxProcessor(db, sender, NullLogger<OutboxProcessor>.Instance);
            Assert.False(await processor.DispatchOneAsync(default));
            Assert.True(await processor.ReplayAsync(id, default));
            Assert.False(await processor.ReplayAsync(id, default));
        }
        sender.Fail = false;
        await using (var db = new TicketFlowDbContext(options))
            await new OutboxProcessor(db, sender, NullLogger<OutboxProcessor>.Instance).DispatchOneAsync(default);
        Assert.Single(sender.Recipients);
    }

    private sealed class CaptureSender : INotificationSender
    {
        public List<string> Recipients { get; } = [];
        public bool Fail { get; set; }
        public Task SendAsync(string address, Guid ticketId, long number, string kind, Guid messageId, CancellationToken ct)
        {
            if (Fail) throw new InvalidOperationException("SMTP unavailable");
            lock (Recipients) Recipients.Add(address);
            return Task.CompletedTask;
        }
    }
}
