using TicketFlow.Application.Administration;
using TicketFlow.Domain.Common;
using TicketFlow.Domain.Tickets;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class AdministrationTests
{
    private readonly Store store = new();
    private readonly Actor customer = new(Guid.NewGuid(), "Customer", "Customer");
    private readonly Actor admin = new(Guid.NewGuid(), "Admin", "Admin");

    [Fact]
    public async Task Only_administrators_can_list_or_change_administration_data()
    {
        var workflow = new AdministrationWorkflow(store);
        var error = await Assert.ThrowsAsync<RuleException>(() => workflow.CategoriesAsync(customer, default));
        Assert.Equal(ErrorKind.Forbidden, error.Kind);
        error = await Assert.ThrowsAsync<RuleException>(() => workflow.ChangeRoleAsync(customer, Guid.NewGuid(), new("Agent"), default));
        Assert.Equal(ErrorKind.Forbidden, error.Kind);
    }

    [Fact]
    public async Task Category_values_are_normalized_and_invalid_slugs_rejected()
    {
        var workflow = new AdministrationWorkflow(store);
        var item = await workflow.CreateCategoryAsync(admin, new("  Billing  ", "  billing-help  "), default);
        Assert.Equal("Billing", item.Name);
        Assert.Equal("billing-help", item.Slug);
        var error = await Assert.ThrowsAsync<RuleException>(() => workflow.CreateCategoryAsync(admin, new("Bad", "Bad slug!"), default));
        Assert.Equal(ErrorKind.Validation, error.Kind);
    }

    [Fact]
    public async Task Role_change_rejects_unknown_roles_and_self_demotion()
    {
        var workflow = new AdministrationWorkflow(store);
        var error = await Assert.ThrowsAsync<RuleException>(() => workflow.ChangeRoleAsync(admin, Guid.NewGuid(), new("Owner"), default));
        Assert.Equal(ErrorKind.Validation, error.Kind);
        error = await Assert.ThrowsAsync<RuleException>(() => workflow.ChangeRoleAsync(admin, admin.Id, new("Customer"), default));
        Assert.Equal(ErrorKind.Conflict, error.Kind);
    }

    private sealed class Store : IAdministrationStore
    {
        public Task<IReadOnlyList<AdminCategoryView>> CategoriesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdminCategoryView>>([]);
        public Task<AdminCategoryView> CreateCategoryAsync(string name, string slug, CancellationToken ct) => Task.FromResult(new AdminCategoryView(Guid.NewGuid(), name, slug));
        public Task<AdminCategoryView> UpdateCategoryAsync(Guid id, string name, string slug, CancellationToken ct) => Task.FromResult(new AdminCategoryView(id, name, slug));
        public Task<IReadOnlyList<AdminUserView>> UsersAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<AdminUserView>>([]);
        public Task<AdminUserView> ChangeRoleAsync(Guid id, string role, CancellationToken ct) => Task.FromResult(new AdminUserView(id, "a@b.test", "Name", role));
    }
}
