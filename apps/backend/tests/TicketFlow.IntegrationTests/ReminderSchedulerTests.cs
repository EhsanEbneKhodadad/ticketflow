using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Notifications;
using TicketFlow.Infrastructure.Persistence;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class ReminderSchedulerTests
{
    [Fact]
    public async Task Reminder_is_deduplicated_and_cancelled_if_ticket_changes()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        var options = new DbContextOptionsBuilder<TicketFlowDbContext>().UseNpgsql(database.ConnectionString).Options;
        Guid ticketId;
        await using (var db = new TicketFlowDbContext(options))
        {
            var customer = await db.Users.SingleAsync(u => u.Email == "customer@ticketflow.local");
            var agent = await db.Users.SingleAsync(u => u.Email == "agent@ticketflow.local");
            var staff = new Actor(agent.Id, agent.DisplayName, "Agent");
            var ticket = Ticket.Create(new(customer.Id, customer.DisplayName, "Customer"), "Valid title", "Valid description", Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941"), [], TicketPriority.Low);
            ticket.ChangeStatus(staff, TicketStatus.InProgress, ticket.Version);
            ticket.ChangeStatus(staff, TicketStatus.WaitingForCustomer, ticket.Version);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            ticketId = ticket.Id;
            var old = DateTimeOffset.UtcNow.AddHours(-25);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tickets SET \"UpdatedAt\" = {old} WHERE \"Id\" = {ticketId}");
            await db.OutboxMessages.ExecuteUpdateAsync(s => s.SetProperty(m => m.CompletedAt, DateTimeOffset.UtcNow));
        }
        var settings = new ConfigurationBuilder().AddInMemoryCollection().Build();
        await using (var db = new TicketFlowDbContext(options))
        {
            var scheduler = new ReminderScheduler(db, settings);
            await scheduler.EnqueueDueAsync(default);
            await scheduler.EnqueueDueAsync(default);
            Assert.Single(await db.OutboxMessages.Where(m => m.Kind == "customerReminder").ToListAsync());
        }
        await using (var db = new TicketFlowDbContext(options))
        {
            var ticket = await db.Tickets.Include(t => t.Audit).SingleAsync(t => t.Id == ticketId);
            var agent = await db.Users.SingleAsync(u => u.Email == "agent@ticketflow.local");
            ticket.ChangeStatus(new(agent.Id, agent.DisplayName, "Agent"), TicketStatus.Resolved, ticket.Version);
            await db.SaveChangesAsync();
            await db.OutboxMessages.Where(m => m.Kind != "customerReminder").ExecuteUpdateAsync(s => s.SetProperty(m => m.CompletedAt, DateTimeOffset.UtcNow));
        }
        var sender = new CaptureSender();
        await using (var db = new TicketFlowDbContext(options))
            await new OutboxProcessor(db, sender, NullLogger<OutboxProcessor>.Instance).DispatchOneAsync(default);
        Assert.Equal(0, sender.Count);
    }

    [Fact]
    public async Task Escalation_falls_back_to_admin_if_assigned_agent_loses_staff_role()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        var options = new DbContextOptionsBuilder<TicketFlowDbContext>().UseNpgsql(database.ConnectionString).Options;
        await using (var db = new TicketFlowDbContext(options))
        {
            var customer = await db.Users.SingleAsync(u => u.Email == "customer@ticketflow.local");
            var agent = await db.Users.SingleAsync(u => u.Email == "agent@ticketflow.local");
            var ticket = Ticket.Create(new(customer.Id, customer.DisplayName, "Customer"), "Valid title", "Valid description", Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941"), [], TicketPriority.Low);
            ticket.Assign(new(agent.Id, agent.DisplayName, "Agent"), agent.Id, agent.DisplayName, ticket.Version);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync();
            var old = DateTimeOffset.UtcNow.AddHours(-49);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE tickets SET \"UpdatedAt\" = {old} WHERE \"Id\" = {ticket.Id}");
            await db.OutboxMessages.ExecuteUpdateAsync(s => s.SetProperty(m => m.CompletedAt, DateTimeOffset.UtcNow));
            var agentRole = await db.Roles.SingleAsync(r => r.Name == "Agent");
            db.UserRoles.Remove(await db.UserRoles.SingleAsync(r => r.UserId == agent.Id && r.RoleId == agentRole.Id));
            await db.SaveChangesAsync();
        }
        await using (var db = new TicketFlowDbContext(options))
            await new ReminderScheduler(db, new ConfigurationBuilder().AddInMemoryCollection().Build()).EnqueueDueAsync(default);
        var sender = new CaptureSender();
        await using (var db = new TicketFlowDbContext(options))
            await new OutboxProcessor(db, sender, NullLogger<OutboxProcessor>.Instance).DispatchOneAsync(default);
        Assert.Equal("admin@ticketflow.local", sender.Address);
    }

    private sealed class CaptureSender : INotificationSender
    {
        public int Count { get; private set; }
        public string? Address { get; private set; }
        public Task SendAsync(string address, Guid ticketId, long number, string kind, Guid messageId, CancellationToken ct)
        {
            Count++;
            Address = address;
            return Task.CompletedTask;
        }
    }
}
