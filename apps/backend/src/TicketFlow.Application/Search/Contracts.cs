using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Application.Search;

public enum SearchSort { Relevance, Newest }
public sealed record TicketSearchQuery(string? Q = null, int Page = 1, int PageSize = 20,
    TicketStatus? Status = null, TicketPriority? Priority = null, Guid? CategoryId = null,
    Guid? AgentId = null, DateOnly? From = null, DateOnly? To = null, SearchSort Sort = SearchSort.Relevance);
public sealed record SearchTextSegment(string Text, bool Highlighted);
public sealed record TicketSearchItem(TicketListItem Ticket, IReadOnlyList<SearchTextSegment> Snippet);
public sealed record TicketSearchPage(IReadOnlyList<TicketSearchItem> Items, int Total, int Page, int PageSize, int CandidateLimit = 1000);
public sealed record SearchTicket(TicketListItem Ticket, string Description, string[] Tags, string[] PublicReplies);
public sealed record PublicTicketDocument(Guid Id, long Number, string TicketNumber, string Title, string Description,
    string[] Tags, string[] PublicReplies, Guid RequesterId, DateTimeOffset CreatedAt,
    TicketStatus Status, TicketPriority Priority, Guid CategoryId, Guid? AgentId);

public interface ITicketSearchIndex
{
    Task<IReadOnlyList<Guid>> SearchAsync(Actor actor, TicketSearchQuery query, CancellationToken ct);
    Task EnsureIndexAsync(CancellationToken ct);
    Task UpsertAsync(IReadOnlyList<PublicTicketDocument> documents, CancellationToken ct);
}
public interface ISearchTicketStore
{
    Task<IReadOnlyList<SearchTicket>> HydrateAsync(Actor actor, TicketSearchQuery query, IReadOnlyList<Guid> ids, CancellationToken ct);
    Task<long> LastNumberAsync(CancellationToken ct);
    Task<IReadOnlyList<PublicTicketDocument>> ReadBatchAsync(long afterNumber, long throughNumber, int size, CancellationToken ct);
}
