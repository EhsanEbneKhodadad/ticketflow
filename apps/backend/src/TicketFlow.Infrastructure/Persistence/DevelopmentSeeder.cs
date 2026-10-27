using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using TicketFlow.Application.Abstractions;
using TicketFlow.Infrastructure.Identity;

namespace TicketFlow.Infrastructure.Persistence;

public sealed class DevelopmentSeeder(TicketFlowDbContext db, UserManager<AppUser> users, IConfiguration config, IHostEnvironment environment) : IDevelopmentSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if ((!environment.IsDevelopment() && !environment.IsEnvironment("Testing")) || !config.GetValue<bool>("Seed:Enabled"))
            throw new InvalidOperationException("Development seed requires an explicit local/testing opt-in.");
        var generalId = Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941");
        var billingId = Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1942");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO categories (id, name, slug)
            VALUES ({generalId}, {"General"}, {"general"}), ({billingId}, {"Billing"}, {"billing"})
            ON CONFLICT (slug) DO NOTHING
            """, cancellationToken);
        var password = config["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password)) return;
        foreach (var role in new[] { "Customer", "Agent", "Admin" })
        {
            var email = role.ToLowerInvariant() + "@ticketflow.local";
            var user = await users.FindByEmailAsync(email);
            if (user is not null) continue;
            user = new AppUser { Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = role == "Customer" ? "Alex Morgan" : role == "Agent" ? "Jordan Lee" : "Sam Rivera" };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded) throw new InvalidOperationException("Development demo password does not satisfy the password policy.");
            if (!(await users.AddToRoleAsync(user, role)).Succeeded) throw new InvalidOperationException("Development role assignment failed.");
        }
    }
}
