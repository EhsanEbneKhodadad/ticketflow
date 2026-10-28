using TicketFlow.Application.Search;
using TicketFlow.Application.Tickets;
using TicketFlow.Domain.Tickets;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class TicketSearchTests
{
    private static readonly Actor Customer = new(Guid.NewGuid(), "Customer", "Customer");

    [Theory]
    [InlineData(0, 20)]
    [InlineData(51, 20)]
    [InlineData(1, 101)]
    [InlineData(1, 0)]
    public async Task Rejects_pages_outside_candidate_window(int page, int size)
    {
        var workflow = new TicketSearchWorkflow(new Candidates([]), new Hydration([]));
        var error = await Assert.ThrowsAsync<RuleException>(() => workflow.SearchAsync(Customer, new(Page: page, PageSize: size), default));
        Assert.Equal(ErrorKind.Validation, error.Kind);
    }

    [Fact]
    public async Task Rejects_overlong_queries_invalid_enums_and_reversed_creation_days()
    {
        var workflow = new TicketSearchWorkflow(new Candidates([]), new Hydration([]));
        foreach (var query in new TicketSearchQuery[] {
            new(Q: new string('x', 161)), new(Sort: (SearchSort)99),
            new(Status: (TicketStatus)99), new(Priority: (TicketPriority)99),
            new(From: new(2026, 10, 29), To: new(2026, 10, 28)) })
        {
            var error = await Assert.ThrowsAsync<RuleException>(() => workflow.SearchAsync(Customer, query, default));
            Assert.Equal(ErrorKind.Validation, error.Kind);
        }
    }

    [Fact]
    public async Task Paginates_only_hydrated_matches_in_candidate_order_and_highlights_current_public_text()
    {
        var first = Ticket("first", "First current title");
        var second = Ticket("second", "Second current title");
        var stale = Guid.NewGuid();
        var workflow = new TicketSearchWorkflow(new Candidates([first.Ticket.Id, stale, second.Ticket.Id, first.Ticket.Id]),
            new Hydration([second, first]));
        var result = await workflow.SearchAsync(Customer, new(Q: "rocket", Page: 2, PageSize: 1), default);
        Assert.Equal(2, result.Total);
        Assert.Equal(second.Ticket.Id, Assert.Single(result.Items).Ticket.Id);
        Assert.Contains(result.Items[0].Snippet, segment => segment.Highlighted && segment.Text == "rocket");
        Assert.Contains("<script>", string.Concat(result.Items[0].Snippet.Select(s => s.Text)));
        Assert.DoesNotContain(result.Items.SelectMany(i => i.Snippet), s => s.Text.Contains("INTERNAL", StringComparison.Ordinal));
        Assert.True(string.Concat(result.Items[0].Snippet.Select(s => s.Text)).Length <= 242);
    }

    [Fact]
    public async Task Snippets_bound_context_on_both_sides_of_a_public_reply_match()
    {
        var ticket = Ticket("TKT-1001", "Current title") with { PublicReplies = [new string('a', 300) + " rocket " + new string('b', 300)] };
        var workflow = new TicketSearchWorkflow(new Candidates([ticket.Ticket.Id]), new Hydration([ticket]));
        var page = await workflow.SearchAsync(Customer, new(Q: "rocket"), default);
        var text = string.Concat(Assert.Single(page.Items).Snippet.Select(s => s.Text));
        Assert.Equal(242, text.Length);
        Assert.StartsWith("\u2026", text);
        Assert.EndsWith("\u2026", text);
    }

    [Fact]
    public async Task Newest_sort_uses_current_database_dates_and_bounds_untrusted_candidates()
    {
        var older = Ticket("older", "Older ticket");
        var newer = Ticket("newer", "Newer ticket");
        newer = newer with { Ticket = newer.Ticket with { CreatedAt = DateTimeOffset.UtcNow.AddDays(1) } };
        var workflow = new TicketSearchWorkflow(new Candidates([older.Ticket.Id, newer.Ticket.Id, .. Enumerable.Range(0, 1100).Select(_ => Guid.NewGuid())]),
            new Hydration([older, newer]));
        var result = await workflow.SearchAsync(Customer, new(Sort: SearchSort.Newest), default);
        Assert.Equal(newer.Ticket.Id, result.Items[0].Ticket.Id);
        Assert.Equal(2, result.Total);
    }

    private static SearchTicket Ticket(string number, string title) => new(
        new TicketListItem(Guid.NewGuid(), number, title, TicketStatus.Open, TicketPriority.Medium,
            "Customer", null, null, "General", 1, DateTimeOffset.UtcNow),
        "Current description without the search term.", [], [new string('a', 300) + " <script>rocket</script> public reply"]);

    private sealed class Candidates(IReadOnlyList<Guid> ids) : ITicketSearchIndex
    {
        public Task<IReadOnlyList<Guid>> SearchAsync(Actor actor, TicketSearchQuery query, CancellationToken ct) => Task.FromResult(ids);
        public Task EnsureIndexAsync(CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(IReadOnlyList<PublicTicketDocument> documents, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Hydration(IReadOnlyList<SearchTicket> items) : ISearchTicketStore
    {
        public Task<IReadOnlyList<SearchTicket>> HydrateAsync(Actor actor, TicketSearchQuery query, IReadOnlyList<Guid> ids, CancellationToken ct)
        {
            Assert.True(ids.Count <= 1000);
            return Task.FromResult(items);
        }
        public Task<long> LastNumberAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PublicTicketDocument>> ReadBatchAsync(long afterNumber, long throughNumber, int size, CancellationToken ct) => throw new NotSupportedException();
    }
}
