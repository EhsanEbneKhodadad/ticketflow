using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TicketFlow.Domain.Categories;
using TicketFlow.Domain.Tickets;
using TicketFlow.Infrastructure.Identity;

namespace TicketFlow.Infrastructure.Persistence;

public sealed class TicketFlowDbContext(DbContextOptions<TicketFlowDbContext> options) : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<SessionFamily> SessionFamilies => Set<SessionFamily>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AddOutboxMessages();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AddOutboxMessages();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AddOutboxMessages()
    {
        ChangeTracker.DetectChanges();
        var pending = ChangeTracker.Entries<OutboxMessage>().Select(e => e.Entity.Id).ToHashSet();
        foreach (var entry in ChangeTracker.Entries<TicketAudit>().Where(e => e.State == EntityState.Added).ToArray())
        {
            var audit = entry.Entity;
            var ticket = ChangeTracker.Entries<Ticket>().Single(t => t.Entity.Id == audit.TicketId).Entity;
            if (pending.Add(audit.Id)) OutboxMessages.Add(new OutboxMessage
            {
                Id = audit.Id,
                TicketId = audit.TicketId,
                ActorId = audit.ActorId,
                RequesterId = ticket.RequesterId,
                AgentId = ticket.AgentId,
                Kind = audit.Kind,
                Internal = audit.Internal,
                CreatedAt = audit.CreatedAt
            });
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<AppUser>().Property(u => u.DisplayName).HasMaxLength(80).IsRequired();
        var roles = new[] { "Customer", "Agent", "Admin" }.Select((name, index) => new IdentityRole<Guid>
        {
            Id = Guid.Parse($"aabed648-e725-4b50-93c1-2cc6394c195{index}"),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            ConcurrencyStamp = name + "-role-v1"
        });
        modelBuilder.Entity<IdentityRole<Guid>>().HasData(roles);

        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.ToTable("outbox_messages");
        outbox.HasKey(m => m.Id);
        outbox.Property(m => m.Id).ValueGeneratedNever();
        outbox.Property(m => m.Kind).HasMaxLength(40).IsRequired();
        outbox.Property(m => m.DedupKey).HasMaxLength(100);
        outbox.HasIndex(m => m.DedupKey).IsUnique();
        outbox.HasIndex(m => new { m.TicketId, m.TicketVersion, m.Kind });
        outbox.HasIndex(m => new { m.CompletedAt, m.FailedAt, m.NextAttemptAt });

        var category = modelBuilder.Entity<Category>();
        category.ToTable("categories");
        category.HasKey(c => c.Id);
        category.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        category.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        category.Property(c => c.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        category.HasIndex(c => c.Slug).IsUnique();

        var family = modelBuilder.Entity<SessionFamily>();
        family.ToTable("session_families");
        family.HasKey(f => f.Id);
        family.Property(f => f.Id).ValueGeneratedNever();
        family.Property(f => f.SecurityStamp).HasMaxLength(256).IsRequired();
        family.HasOne<AppUser>().WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);
        var session = modelBuilder.Entity<RefreshSession>();
        session.ToTable("refresh_sessions");
        session.HasKey(s => s.Id);
        session.Property(s => s.Id).ValueGeneratedNever();
        session.Property(s => s.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        session.HasIndex(s => s.TokenHash).IsUnique();
        session.HasIndex(s => new { s.UserId, s.FamilyId });
        session.HasOne<SessionFamily>().WithMany().HasForeignKey(s => s.FamilyId).OnDelete(DeleteBehavior.Cascade);
        session.HasOne<AppUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);

        var ticket = modelBuilder.Entity<Ticket>();
        ticket.ToTable("tickets");
        ticket.HasKey(t => t.Id);
        ticket.Property(t => t.Id).ValueGeneratedNever();
        ticket.Property(t => t.Number).UseIdentityAlwaysColumn().HasIdentityOptions(startValue: 1001);
        ticket.HasIndex(t => t.Number).IsUnique();
        ticket.HasIndex(t => new { t.RequesterId, t.CreatedAt });
        ticket.HasIndex(t => new { t.AgentId, t.Status });
        ticket.Property(t => t.Title).HasMaxLength(160).IsRequired();
        ticket.Property(t => t.Description).HasMaxLength(10000).IsRequired();
        ticket.Property(t => t.RequesterName).HasMaxLength(80).IsRequired();
        ticket.Property(t => t.AgentName).HasMaxLength(80);
        ticket.Property(t => t.Tags).HasColumnType("text[]");
        ticket.Property(t => t.Version).IsConcurrencyToken();
        ticket.Property(t => t.Status).HasConversion<string>().HasMaxLength(30);
        ticket.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
        ticket.HasOne<Category>().WithMany().HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<AppUser>().WithMany().HasForeignKey(t => t.RequesterId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasOne<AppUser>().WithMany().HasForeignKey(t => t.AgentId).OnDelete(DeleteBehavior.Restrict);
        ticket.HasMany(t => t.Messages).WithOne().HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Cascade);
        ticket.Navigation(t => t.Messages).HasField("_messages").UsePropertyAccessMode(PropertyAccessMode.Field);
        ticket.HasMany(t => t.Audit).WithOne().HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Cascade);
        ticket.Navigation(t => t.Audit).HasField("_audit").UsePropertyAccessMode(PropertyAccessMode.Field);
        ticket.HasMany(t => t.Attachments).WithOne().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Cascade);
        ticket.Navigation(t => t.Attachments).HasField("_attachments").UsePropertyAccessMode(PropertyAccessMode.Field);
        var attachment = modelBuilder.Entity<TicketAttachment>();
        attachment.ToTable("ticket_attachments", table => table.HasCheckConstraint("CK_attachment_size", "\"Size\" > 0 AND \"Size\" <= 10485760"));
        attachment.HasKey(a => a.Id);
        attachment.Property(a => a.Id).ValueGeneratedNever();
        attachment.Property(a => a.FileName).HasMaxLength(180).IsRequired();
        attachment.Property(a => a.ContentType).HasMaxLength(100).IsRequired();
        attachment.Property(a => a.ObjectKey).HasMaxLength(200).IsRequired();
        attachment.HasIndex(a => a.ObjectKey).IsUnique();
        attachment.HasIndex(a => new { a.TicketId, a.CreatedAt });
        attachment.HasOne<TicketMessage>().WithMany().HasForeignKey(a => new { a.MessageId, a.TicketId })
            .HasPrincipalKey(m => new { m.Id, m.TicketId }).OnDelete(DeleteBehavior.Restrict);
        var message = modelBuilder.Entity<TicketMessage>();
        message.ToTable("ticket_messages");
        message.HasKey(m => m.Id);
        message.Property(m => m.Id).ValueGeneratedNever();
        message.Property(m => m.Body).HasMaxLength(10000);
        message.Property(m => m.AuthorName).HasMaxLength(80);
        message.Property(m => m.AuthorRole).HasMaxLength(20);
        var audit = modelBuilder.Entity<TicketAudit>();
        audit.ToTable("ticket_audit");
        audit.HasKey(a => a.Id);
        audit.Property(a => a.Id).ValueGeneratedNever();
        audit.Property(a => a.ActorName).HasMaxLength(80);
        audit.Property(a => a.Kind).HasMaxLength(40);
        audit.Property(a => a.Summary).HasMaxLength(300);
    }
}
