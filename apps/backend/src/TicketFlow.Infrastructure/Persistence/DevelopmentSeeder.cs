using Microsoft.EntityFrameworkCore;
using TicketFlow.Application.Abstractions;

namespace TicketFlow.Infrastructure.Persistence;

public sealed class DevelopmentSeeder(TicketFlowDbContext db) : IDevelopmentSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var generalId = Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1941");
        var billingId = Guid.Parse("aabed648-e725-4b50-93c1-2cc6394c1942");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO categories (id, name, slug)
            VALUES ({generalId}, {"General"}, {"general"}), ({billingId}, {"Billing"}, {"billing"})
            ON CONFLICT (slug) DO NOTHING
            """, cancellationToken);
    }
}
