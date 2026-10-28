using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TicketFlow.Application.Administration;
using TicketFlow.Domain.Categories;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Identity;
using TicketFlow.Infrastructure.Persistence;

namespace TicketFlow.Infrastructure.Administration;

public sealed class AdministrationStore(TicketFlowDbContext db, UserManager<AppUser> users) : IAdministrationStore, IDashboardStore
{
    public async Task<DashboardView> ReadAsync(Guid? requesterId, CancellationToken ct)
    {
        var query = db.Tickets.AsNoTracking();
        if (requesterId.HasValue) query = query.Where(t => t.RequesterId == requesterId.Value);
        var values = await query.GroupBy(_ => 1).Select(g => new DashboardView(
            g.Count(), g.Count(t => t.Status == TicketStatus.Open), g.Count(t => t.Status == TicketStatus.InProgress),
            g.Count(t => t.Status == TicketStatus.WaitingForCustomer), g.Count(t => t.Status == TicketStatus.Resolved),
            g.Count(t => t.Status == TicketStatus.Closed), g.Count(t => t.AgentId == null),
            g.Count(t => t.Priority == TicketPriority.High || t.Priority == TicketPriority.Urgent))).SingleOrDefaultAsync(ct);
        return values ?? new DashboardView(0, 0, 0, 0, 0, 0, 0, 0);
    }
    public async Task<IReadOnlyList<AdminCategoryView>> CategoriesAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking().OrderBy(c => c.Name).Select(c => new AdminCategoryView(c.Id, c.Name, c.Slug)).ToArrayAsync(ct);
    public async Task<AdminCategoryView> CreateCategoryAsync(string name, string slug, CancellationToken ct)
    {
        var category = new Category(Guid.NewGuid(), name, slug);
        db.Categories.Add(category);
        await SaveCategoryAsync(ct);
        return new(category.Id, category.Name, category.Slug);
    }
    public async Task<AdminCategoryView> UpdateCategoryAsync(Guid id, string name, string slug, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new RuleException(ErrorKind.NotFound, "Category not found.");
        category.Rename(name, slug);
        await SaveCategoryAsync(ct);
        return new(category.Id, category.Name, category.Slug);
    }
    private async Task SaveCategoryAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new RuleException(ErrorKind.Conflict, "This category slug is already in use."); }
    }
    public async Task<IReadOnlyList<AdminUserView>> UsersAsync(CancellationToken ct) =>
        await (from user in db.Users.AsNoTracking()
               join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
               join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
               orderby user.DisplayName, user.Id
               select new AdminUserView(user.Id, user.Email!, user.DisplayName, role.Name!)).ToArrayAsync(ct);
    public async Task<AdminUserView> ChangeRoleAsync(Guid id, string role, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize changes to the administrator set, including across API instances.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1999092905)", ct);
        var user = await users.FindByIdAsync(id.ToString()) ?? throw new RuleException(ErrorKind.NotFound, "User not found.");
        var current = await users.GetRolesAsync(user);
        if (current.Count != 1) throw new RuleException(ErrorKind.Conflict, "User has an unsupported role configuration.");
        if (current[0] == role) return new(user.Id, user.Email!, user.DisplayName, role);
        if (current[0] == "Admin" && (await users.GetUsersInRoleAsync("Admin")).Count <= 1)
            throw new RuleException(ErrorKind.Conflict, "At least one administrator is required.");
        if (role == "Customer" && await db.Tickets.AnyAsync(t => t.AgentId == id && t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved, ct))
            throw new RuleException(ErrorKind.Conflict, "Reassign this user's active tickets before changing their role.");
        if (!(await users.RemoveFromRoleAsync(user, current[0])).Succeeded || !(await users.AddToRoleAsync(user, role)).Succeeded
            || !(await users.UpdateSecurityStampAsync(user)).Succeeded)
            throw new RuleException(ErrorKind.Conflict, "The role could not be changed.");
        await tx.CommitAsync(ct);
        return new(user.Id, user.Email!, user.DisplayName, role);
    }
}
