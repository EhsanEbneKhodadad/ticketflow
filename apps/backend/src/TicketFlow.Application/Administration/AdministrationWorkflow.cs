using System.Text.RegularExpressions;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Application.Administration;

public sealed class AdministrationWorkflow(IAdministrationStore store)
{
    public Task<IReadOnlyList<AdminCategoryView>> CategoriesAsync(Actor actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        return store.CategoriesAsync(ct);
    }
    public Task<IReadOnlyList<AdminUserView>> UsersAsync(Actor actor, CancellationToken ct)
    {
        RequireAdmin(actor);
        return store.UsersAsync(ct);
    }
    public Task<AdminCategoryView> CreateCategoryAsync(Actor actor, CategoryRequest input, CancellationToken ct)
    {
        RequireAdmin(actor);
        var (name, slug) = Validate(input);
        return store.CreateCategoryAsync(name, slug, ct);
    }
    public Task<AdminCategoryView> UpdateCategoryAsync(Actor actor, Guid id, CategoryRequest input, CancellationToken ct)
    {
        RequireAdmin(actor);
        var (name, slug) = Validate(input);
        return store.UpdateCategoryAsync(id, name, slug, ct);
    }
    public Task<AdminUserView> ChangeRoleAsync(Actor actor, Guid id, ChangeRoleRequest input, CancellationToken ct)
    {
        RequireAdmin(actor);
        if (input.Role is not ("Customer" or "Agent" or "Admin"))
            throw new RuleException(ErrorKind.Validation, "Choose a valid role.");
        if (actor.Id == id && input.Role != "Admin")
            throw new RuleException(ErrorKind.Conflict, "You cannot remove your own administrator role.");
        return store.ChangeRoleAsync(id, input.Role, ct);
    }
    private static (string Name, string Slug) Validate(CategoryRequest input)
    {
        var name = input.Name?.Trim() ?? "";
        var slug = input.Slug?.Trim() ?? "";
        if (name.Length is < 1 or > 100 || slug.Length is < 1 or > 100 || !Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            throw new RuleException(ErrorKind.Validation, "Provide a name and a lowercase slug with letters, numbers, or hyphens.");
        return (name, slug);
    }
    private static void RequireAdmin(Actor actor)
    {
        if (actor.Role != "Admin") throw new RuleException(ErrorKind.Forbidden, "This action requires an administrator.");
    }
}

public sealed class DashboardWorkflow(IDashboardStore store)
{
    public Task<DashboardView> ReadAsync(Actor actor, CancellationToken ct) =>
        store.ReadAsync(actor.IsStaff ? null : actor.Id, ct);
}
