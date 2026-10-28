using System.Text.RegularExpressions;
using TicketFlow.Domain.Tickets;

namespace TicketFlow.Application.Search;

public sealed class TicketSearchWorkflow(ITicketSearchIndex index, ISearchTicketStore store)
{
    public const int CandidateLimit = 1000;

    public async Task<TicketSearchPage> SearchAsync(Actor actor, TicketSearchQuery query, CancellationToken ct)
    {
        if (query.Page is < 1 or > CandidateLimit || query.PageSize is < 1 or > 100
            || (query.Page - 1) * query.PageSize >= CandidateLimit || query.Q?.Length > 160
            || !Enum.IsDefined(query.Sort) || (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            || (query.Priority.HasValue && !Enum.IsDefined(query.Priority.Value)) || query.From > query.To)
            throw new RuleException(ErrorKind.Validation, "Use a query up to 160 characters, valid filters, an inclusive creation-date interval, and pages within the first 1000 candidates (page size up to 100).");

        var ids = (await index.SearchAsync(actor, query, ct)).Take(CandidateLimit).Distinct().ToArray();
        var hydrated = (await store.HydrateAsync(actor, query, ids, ct)).ToDictionary(t => t.Ticket.Id);
        var matches = ids.Where(hydrated.ContainsKey).Select(id => hydrated[id]);
        if (query.Sort == SearchSort.Newest)
            matches = matches.OrderByDescending(t => t.Ticket.CreatedAt);
        var authorized = matches.ToArray();
        var items = authorized.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(t => new TicketSearchItem(t.Ticket, Snippet(t, query.Q))).ToArray();
        return new TicketSearchPage(items, authorized.Length, query.Page, query.PageSize, CandidateLimit);
    }

    private static IReadOnlyList<SearchTextSegment> Snippet(SearchTicket ticket, string? query)
    {
        var terms = Regex.Matches(query ?? "", @"[\p{L}\p{N}]+")
            .Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(t => t.Length).ToArray();
        var sources = new[] { ticket.Description, ticket.Ticket.Title, ticket.Ticket.Number }
            .Concat(ticket.Tags).Concat(ticket.PublicReplies);
        var text = sources.FirstOrDefault(s => terms.Any(t => s.Contains(t, StringComparison.OrdinalIgnoreCase))) ?? ticket.Description;
        var position = terms.Select(t => text.IndexOf(t, StringComparison.OrdinalIgnoreCase)).Where(p => p >= 0).DefaultIfEmpty(0).Min();
        var start = Math.Max(0, position - 80);
        var length = Math.Min(240, text.Length - start);
        var excerpt = (start > 0 ? "\u2026" : "") + text.Substring(start, length) + (start + length < text.Length ? "\u2026" : "");
        if (terms.Length == 0) return [new(excerpt, false)];
        var segments = new List<SearchTextSegment>();
        var offset = 0;
        foreach (Match match in Regex.Matches(excerpt, string.Join("|", terms.Select(Regex.Escape)), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking))
        {
            if (match.Index > offset) segments.Add(new(excerpt[offset..match.Index], false));
            segments.Add(new(match.Value, true));
            offset = match.Index + match.Length;
        }
        if (offset < excerpt.Length) segments.Add(new(excerpt[offset..], false));
        return segments;
    }
}
