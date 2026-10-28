using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TicketFlow.Infrastructure.Identity;

public sealed record AuthSettings(string SigningKey, string Issuer, string Audience)
{
    public static AuthSettings Load(IConfiguration config, IHostEnvironment environment)
    {
        var local = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        var key = config["Auth:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) && local)
            key = "local-development-only-ticketflow-signing-key-never-use-in-production-2026";
        if (key is null || key.Length < 64 || (!local && key.StartsWith("local-", StringComparison.Ordinal)))
            throw new InvalidOperationException("Configure a private Auth:SigningKey of at least 64 characters.");
        return new AuthSettings(key, "TicketFlow", "TicketFlow.Web");
    }
}
