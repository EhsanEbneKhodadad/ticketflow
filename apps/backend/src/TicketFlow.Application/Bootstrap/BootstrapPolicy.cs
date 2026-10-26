namespace TicketFlow.Application.Bootstrap;

public static class BootstrapPolicy
{
    public static string? GetError(bool migrate, bool seed, bool development, bool enabled)
    {
        if (!seed) return null;
        if (!migrate) return "Seeding requires --migrate.";
        if (!development) return "Seeding is allowed only in Development.";
        return enabled ? null : "Seeding requires Seed:Enabled=true.";
    }
}
