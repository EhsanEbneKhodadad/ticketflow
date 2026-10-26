using TicketFlow.Application.Abstractions;
using TicketFlow.Domain.Categories;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_does_not_reference_framework_or_outer_layers()
    {
        var names = typeof(Category).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(names, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            || n.StartsWith("TicketFlow.", StringComparison.Ordinal));
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_hosts()
    {
        var names = typeof(IDevelopmentSeeder).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(names, n => n is "TicketFlow.Infrastructure" or "TicketFlow.Api" or "TicketFlow.Worker"
            || n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || n.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }
}
