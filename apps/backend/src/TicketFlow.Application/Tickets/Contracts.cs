using TicketFlow.Domain.Tickets;

namespace TicketFlow.Application.Tickets;

public sealed record CreateTicketRequest(string Title, string Description, Guid CategoryId, string[]? Tags, TicketPriority Priority = TicketPriority.Medium);
public sealed record EditTicketRequest(string Title, string Description, Guid CategoryId, string[]? Tags, int ExpectedVersion);
public sealed record MessageRequest(string Body, bool Internal, int ExpectedVersion);
public sealed record StatusRequest(TicketStatus Status, int ExpectedVersion);
public sealed record PriorityRequest(TicketPriority Priority, int ExpectedVersion);
public sealed record AssignmentRequest(Guid? AgentId, int ExpectedVersion);
public sealed record CategoryView(Guid Id, string Name);
public sealed record AgentView(Guid Id, string Name);
public sealed record TicketQuery(int Page = 1, int PageSize = 20, TicketStatus? Status = null, TicketPriority? Priority = null, Guid? AgentId = null, bool Unassigned = false, string? Q = null);
public sealed record MessageView(Guid Id, Guid AuthorId, string AuthorName, string AuthorRole, string Body, bool Internal, DateTimeOffset CreatedAt);
public sealed record AuditView(Guid Id, string ActorName, string Kind, string Summary, DateTimeOffset CreatedAt);
public sealed record TicketListItem(Guid Id, string Number, string Title, TicketStatus Status, TicketPriority Priority, string RequesterName, string? AgentName, Guid? AgentId, string Category, int Version, DateTimeOffset CreatedAt);
public sealed record TicketPage(IReadOnlyList<TicketListItem> Items, int Total, int Page, int PageSize);
public sealed record TicketView(Guid Id, string Number, string Title, string Description, Guid RequesterId, string RequesterName, Guid CategoryId, string Category, string[] Tags, TicketStatus Status, TicketPriority Priority, Guid? AgentId, string? AgentName, int Version, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<MessageView> Messages, IReadOnlyList<AuditView> Audit);
public interface ITicketStore
{
    Task<Ticket?> FindAsync(Guid id, CancellationToken ct);
    Task<TicketPage> ListAsync(Actor actor, TicketQuery query, CancellationToken ct);
    Task<IReadOnlyList<CategoryView>> CategoriesAsync(CancellationToken ct);
    Task<IReadOnlyList<AgentView>> AgentsAsync(CancellationToken ct);
    Task AddAsync(Ticket ticket, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
