using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Categories;

namespace TicketFlow.Infrastructure.Persistence;

public sealed class TicketFlowDbContext(DbContextOptions<TicketFlowDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var category = modelBuilder.Entity<Category>();
        category.ToTable("categories");
        category.HasKey(c => c.Id);
        category.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        category.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        category.Property(c => c.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        category.HasIndex(c => c.Slug).IsUnique();
    }
}
