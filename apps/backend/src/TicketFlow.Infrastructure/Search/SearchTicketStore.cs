using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Search;
using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Search;

public sealed class SearchTicketStore(TicketFlowDbContext db) : ISearchTicketStore
{
    public async Task<IReadOnlyList<SearchTicket>> HydrateAsync(Actor actor, TicketSearchQuery query, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var bounded = ids.Take(TicketSearchWorkflow.CandidateLimit).ToArray();
        var tickets = db.Tickets.AsNoTracking().Where(t => bounded.Contains(t.Id));
        if (!actor.IsStaff) tickets = tickets.Where(t => t.RequesterId == actor.Id);
        if (query.Status.HasValue) tickets = tickets.Where(t => t.Status == query.Status);
        if (query.Priority.HasValue) tickets = tickets.Where(t => t.Priority == query.Priority);
        if (query.CategoryId.HasValue) tickets = tickets.Where(t => t.CategoryId == query.CategoryId);
        if (query.AgentId.HasValue) tickets = tickets.Where(t => t.AgentId == query.AgentId);
        if (query.From.HasValue)
        {
            var from = new DateTimeOffset(query.From.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            tickets = tickets.Where(t => t.CreatedAt >= from);
        }
        if (query.To.HasValue && query.To.Value < DateOnly.MaxValue)
        {
            var until = new DateTimeOffset(query.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            tickets = tickets.Where(t => t.CreatedAt < until);
        }
        return await tickets.AsSplitQuery().Select(t => new SearchTicket(
            new TicketListItem(t.Id, "TKT-" + t.Number, t.Title, t.Status, t.Priority, t.RequesterName,
                t.AgentName, t.AgentId, db.Categories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).First(), t.Version, t.CreatedAt),
            t.Description, t.Tags, t.Messages.Where(m => !m.Internal).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Select(m => m.Body).ToArray()))
            .ToArrayAsync(ct);
    }

    public async Task<long> LastNumberAsync(CancellationToken ct) => await db.Tickets.MaxAsync(t => (long?)t.Number, ct) ?? 0;

    public async Task<IReadOnlyList<PublicTicketDocument>> ReadBatchAsync(long afterNumber, long throughNumber, int size, CancellationToken ct) =>
        await db.Tickets.AsNoTracking().AsSplitQuery().Where(t => t.Number > afterNumber && t.Number <= throughNumber)
            .OrderBy(t => t.Number).Take(Math.Clamp(size, 1, 100))
            .Select(t => new PublicTicketDocument(t.Id, t.Number, "TKT-" + t.Number, t.Title, t.Description, t.Tags,
                t.Messages.Where(m => !m.Internal).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).Select(m => m.Body).ToArray(), t.RequesterId, t.CreatedAt, t.Status, t.Priority, t.CategoryId, t.AgentId))
            .ToArrayAsync(ct);
}
