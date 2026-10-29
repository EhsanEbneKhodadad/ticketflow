using TicketFlow.Application.Administration;
using TicketFlow.Domain.Tickets;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class DashboardTests
{
    [Fact]
    public async Task Customers_receive_only_their_own_scope_and_staff_see_the_full_scope()
    {
        var store = new ScopeStore();
        var workflow = new DashboardWorkflow(store);
        var customer = new Actor(Guid.NewGuid(), "Customer", "Customer");
        await workflow.ReadAsync(customer, default);
        Assert.Equal(customer.Id, store.Requester);
        await workflow.ReadAsync(new Actor(Guid.NewGuid(), "Agent", "Agent"), default);
        Assert.Null(store.Requester);
    }
    private sealed class ScopeStore : IDashboardStore
    {
        public Guid? Requester { get; private set; }
        public Task<DashboardView> ReadAsync(Guid? requesterId, CancellationToken ct)
        {
            Requester = requesterId;
            return Task.FromResult(new DashboardView(0, 0, 0, 0, 0, 0, 0, 0));
        }
    }
}
