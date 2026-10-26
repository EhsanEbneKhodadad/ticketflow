using TicketFlow.Application.Bootstrap;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class BootstrapPolicyTests
{
    [Theory]
    [InlineData(false, true, true, true, "Seeding requires --migrate.")]
    [InlineData(true, true, false, true, "Seeding is allowed only in Development.")]
    [InlineData(true, true, true, false, "Seeding requires Seed:Enabled=true.")]
    public void Rejects_unsafe_seed_requests(bool migrate, bool seed, bool development, bool enabled, string error)
        => Assert.Equal(error, BootstrapPolicy.GetError(migrate, seed, development, enabled));

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, true, true)]
    public void Allows_normal_startup_migration_and_explicit_development_seed(bool migrate, bool seed, bool development, bool enabled)
        => Assert.Null(BootstrapPolicy.GetError(migrate, seed, development, enabled));
}
