namespace TicketFlow.Application.Administration;

public sealed record AdminCategoryView(Guid Id, string Name, string Slug);
public sealed record CategoryRequest(string Name, string Slug);
public sealed record AdminUserView(Guid Id, string Email, string Name, string Role);
public sealed record ChangeRoleRequest(string Role);
public sealed record DashboardView(int Total, int Open, int InProgress, int WaitingForCustomer, int Resolved, int Closed, int Unassigned, int HighPriority);

public interface IAdministrationStore
{
    Task<IReadOnlyList<AdminCategoryView>> CategoriesAsync(CancellationToken ct);
    Task<AdminCategoryView> CreateCategoryAsync(string name, string slug, CancellationToken ct);
    Task<AdminCategoryView> UpdateCategoryAsync(Guid id, string name, string slug, CancellationToken ct);
    Task<IReadOnlyList<AdminUserView>> UsersAsync(CancellationToken ct);
    Task<AdminUserView> ChangeRoleAsync(Guid id, string role, CancellationToken ct);
}
public interface IDashboardStore
{
    Task<DashboardView> ReadAsync(Guid? requesterId, CancellationToken ct);
}
