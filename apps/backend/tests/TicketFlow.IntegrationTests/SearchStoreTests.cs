using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TicketFlow.Application.Search;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Persistence;
using TicketFlow.Infrastructure.Search;
using Xunit;

namespace TicketFlow.IntegrationTests;

[Trait("Category", "Docker")]
public sealed class SearchStoreTests
{
    private static readonly Guid General = Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941");

    [Fact]
    public async Task Database_rechecks_stale_ownership_and_every_filter_before_counting_or_paging()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var users = await db.Users.ToArrayAsync();
        var customer = new Actor(users.Single(u => u.Email == "customer@ticketflow.local").Id, "Customer", "Customer");
        var agent = new Actor(users.Single(u => u.Email == "agent@ticketflow.local").Id, "Agent", "Agent");
        var first = Ticket.Create(customer, "Rocket account issue", "Current public description", General, ["rocket"], TicketPriority.High);
        first.ChangeStatus(agent, TicketStatus.InProgress, first.Version);
        first.Assign(agent, agent.Id, "Agent", first.Version);
        first.Reply(agent, "INTERNAL secret rocket", true, first.Version);
        first.Reply(agent, "Public rocket workaround", false, first.Version);
        var other = Ticket.Create(agent, "Foreign private rocket", "Foreign customer description", General, [], TicketPriority.High);
        var second = Ticket.Create(customer, "Second rocket ticket", "Second public description", General, [], TicketPriority.Low);
        db.Tickets.AddRange(first, other, second);
        await db.SaveChangesAsync();
        var workflow = new TicketSearchWorkflow(new CandidateIndex([first.Id, other.Id, second.Id]), new SearchTicketStore(db));
        var page = await workflow.SearchAsync(customer, new(Q: "rocket", Page: 2, PageSize: 1), default);
        Assert.Equal(2, page.Total);
        Assert.Equal(second.Id, Assert.Single(page.Items).Ticket.Id);
        Assert.DoesNotContain("Foreign", JsonSerializer.Serialize(page));
        var today = DateOnly.FromDateTime(first.CreatedAt.UtcDateTime);
        var allFilters = new TicketSearchQuery(Q: "rocket", Status: TicketStatus.InProgress, Priority: TicketPriority.High,
            CategoryId: General, AgentId: agent.Id, From: today, To: today);
        Assert.Equal(first.Id, Assert.Single((await workflow.SearchAsync(customer, allFilters, default)).Items).Ticket.Id);
        Assert.Equal(2, (await workflow.SearchAsync(customer, new(To: DateOnly.MaxValue), default)).Total);
        foreach (var query in new[] { allFilters with { Status = TicketStatus.Closed }, allFilters with { Priority = TicketPriority.Urgent },
            allFilters with { CategoryId = Guid.NewGuid() }, allFilters with { AgentId = Guid.NewGuid() },
            allFilters with { From = today.AddDays(1), To = null }, allFilters with { From = null, To = today.AddDays(-1) } })
            Assert.Equal(0, (await workflow.SearchAsync(customer, query, default)).Total);
        await db.Tickets.Where(t => t.Id == first.Id).ExecuteUpdateAsync(updates => updates.SetProperty(t => t.RequesterId, agent.Id));
        var afterOwnershipChange = await workflow.SearchAsync(customer, new(Q: "rocket"), default);
        Assert.Equal(1, afterOwnershipChange.Total);
        Assert.Equal(second.Id, Assert.Single(afterOwnershipChange.Items).Ticket.Id);
        var staff = await workflow.SearchAsync(agent, new(Q: "INTERNAL"), default);
        Assert.Equal(3, staff.Total);
        Assert.DoesNotContain("INTERNAL", JsonSerializer.Serialize(staff));
    }

    [Fact]
    public async Task Projection_batches_read_only_public_fields_and_respect_the_replay_high_water_mark()
    {
        await using var database = await PostgresDatabase.CreateAsync();
        await using var app = new WorkflowFactory(database.ConnectionString);
        await app.InitializeAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == "agent@ticketflow.local");
        var agent = new Actor(user.Id, "Agent", "Agent");
        var first = Ticket.Create(agent, "Projection title", "Projection description", General, ["public-tag"], TicketPriority.Medium);
        first.Reply(agent, "INTERNAL diagnosis", true, first.Version);
        first.Reply(agent, "Public reply text", false, first.Version);
        first.Attach(agent, "INTERNAL-file.txt", "text/plain", 12, "PRIVATE-KEY", first.Messages[0].Id, first.Version);
        var second = Ticket.Create(agent, "Second projection", "Second description", General, [], TicketPriority.Low);
        db.Tickets.Add(first);
        await db.SaveChangesAsync();
        db.Tickets.Add(second);
        await db.SaveChangesAsync();
        var store = new SearchTicketStore(db);
        Assert.Equal(second.Number, await store.LastNumberAsync(default));
        var batch = await store.ReadBatchAsync(0, second.Number, 1, default);
        var document = Assert.Single(batch);
        Assert.Equal(first.Id, document.Id);
        Assert.Equal("TKT-1001", document.TicketNumber);
        Assert.Equal("Public reply text", Assert.Single(document.PublicReplies));
        Assert.Equal(TicketStatus.Open, document.Status);
        Assert.Equal(TicketPriority.Medium, document.Priority);
        Assert.Equal(General, document.CategoryId);
        Assert.Null(document.AgentId);
        Assert.DoesNotContain("INTERNAL", JsonSerializer.Serialize(document));
        Assert.DoesNotContain("PRIVATE-KEY", JsonSerializer.Serialize(document));
        Assert.Empty(await store.ReadBatchAsync(first.Number, first.Number, 100, default));
        Assert.Equal(second.Id, Assert.Single(await store.ReadBatchAsync(first.Number, second.Number, 100, default)).Id);
    }

    internal sealed class CandidateIndex(IReadOnlyList<Guid> ids) : ITicketSearchIndex
    {
        public Task<IReadOnlyList<Guid>> SearchAsync(Actor actor, TicketSearchQuery query, CancellationToken ct) => Task.FromResult(ids);
        public Task EnsureIndexAsync(CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(IReadOnlyList<PublicTicketDocument> documents, CancellationToken ct) => Task.CompletedTask;
    }
}
