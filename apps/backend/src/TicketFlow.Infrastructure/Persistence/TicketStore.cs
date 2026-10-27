using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Infrastructure.Persistence;

public sealed class TicketStore(TicketFlowDbContext db) : ITicketStore
{
    public Task<Ticket?> FindAsync(Guid id, CancellationToken ct) =>
        db.Tickets.Include(t => t.Messages).Include(t => t.Audit).AsSplitQuery().SingleOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TicketPage> ListAsync(Actor actor, TicketQuery query, CancellationToken ct)
    {
        var tickets = db.Tickets.AsNoTracking().AsQueryable();
        if (!actor.IsStaff) tickets = tickets.Where(t => t.RequesterId == actor.Id);
        if (query.Status.HasValue) tickets = tickets.Where(t => t.Status == query.Status);
        if (query.Priority.HasValue) tickets = tickets.Where(t => t.Priority == query.Priority);
        if (query.AgentId.HasValue) tickets = tickets.Where(t => t.AgentId == query.AgentId);
        if (query.Unassigned) tickets = tickets.Where(t => t.AgentId == null);
        var text = query.Q?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            var number = long.TryParse(text.Replace("TKT-", "", StringComparison.OrdinalIgnoreCase), out var parsed) ? parsed : -1;
            var pattern = "%" + text + "%";
            tickets = tickets.Where(t => t.Number == number || EF.Functions.ILike(t.Title, pattern));
        }
        var total = await tickets.CountAsync(ct);
        var items = await tickets.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Number)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(t => new TicketListItem(t.Id, "TKT-" + t.Number, t.Title, t.Status, t.Priority, t.RequesterName,
                t.AgentName, t.AgentId, db.Categories.Where(c => c.Id == t.CategoryId).Select(c => c.Name).First(), t.Version, t.CreatedAt))
            .ToArrayAsync(ct);
        return new TicketPage(items, total, query.Page, query.PageSize);
    }

    public async Task<IReadOnlyList<CategoryView>> CategoriesAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new CategoryView(c.Id, c.Name)).ToArrayAsync(ct);

    public async Task<IReadOnlyList<AgentView>> AgentsAsync(CancellationToken ct)
    {
        var roleIds = db.Roles.Where(r => r.Name == "Agent" || r.Name == "Admin").Select(r => r.Id);
        var userIds = db.UserRoles.Where(ur => roleIds.Contains(ur.RoleId)).Select(ur => ur.UserId);
        return await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).OrderBy(u => u.DisplayName)
            .Select(u => new AgentView(u.Id, u.DisplayName)).ToArrayAsync(ct);
    }

    public async Task AddAsync(Ticket ticket, CancellationToken ct) => await db.Tickets.AddAsync(ticket, ct);

    public async Task SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new RuleException(ErrorKind.Conflict, "This ticket changed. Refresh before trying again."); }
    }
}
