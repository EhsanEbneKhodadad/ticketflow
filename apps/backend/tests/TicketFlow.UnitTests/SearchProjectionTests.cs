using TicketFlow.Application.Search;
using TicketFlow.Domain.Tickets;
using Xunit;

namespace TicketFlow.UnitTests;

public sealed class SearchProjectionTests
{
    [Fact]
    public async Task Replay_is_bounded_idempotent_and_rebuilds_a_lost_index_including_current_edits()
    {
        var source = new Source();
        var index = new Index();
        var workflow = new SearchProjectionWorkflow(source, index);
        await workflow.ReconcileAsync(default);
        Assert.Equal(201, index.Documents.Count);
        Assert.Equal(3, index.Batches);
        source.Documents[0] = source.Documents[0] with { Title = "Edited title", PublicReplies = ["New public answer"] };
        await workflow.ReconcileAsync(default);
        Assert.Equal(201, index.Documents.Count);
        Assert.Equal("Edited title", index.Documents[source.Documents[0].Id].Title);
        index.Documents.Clear();
        await workflow.ReconcileAsync(default);
        Assert.Equal(201, index.Documents.Count);
        Assert.Equal("New public answer", Assert.Single(index.Documents[source.Documents[0].Id].PublicReplies));
    }

    [Fact]
    public async Task Failures_are_visible_to_the_worker_and_cancellation_stops_replay()
    {
        var source = new Source();
        var index = new Index { Fail = true };
        var workflow = new SearchProjectionWorkflow(source, index);
        Assert.Equal(ErrorKind.Unavailable, (await Assert.ThrowsAsync<RuleException>(() => workflow.ReconcileAsync(default))).Kind);
        using var ct = new CancellationTokenSource();
        ct.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workflow.ReconcileAsync(ct.Token));
    }

    private sealed class Source : ISearchTicketStore
    {
        public List<PublicTicketDocument> Documents { get; } = Enumerable.Range(1, 201).Select(n =>
            new PublicTicketDocument(Guid.NewGuid(), n, "TKT-" + n, "Title", "Description", [], [], Guid.NewGuid(), DateTimeOffset.UtcNow, TicketStatus.Open, TicketPriority.Medium, Guid.NewGuid(), null)).ToList();
        public Task<long> LastNumberAsync(CancellationToken ct) => Task.FromResult(201L);
        public Task<IReadOnlyList<PublicTicketDocument>> ReadBatchAsync(long afterNumber, long throughNumber, int size, CancellationToken ct)
        {
            Assert.InRange(size, 1, 100);
            Assert.Equal(201, throughNumber);
            return Task.FromResult<IReadOnlyList<PublicTicketDocument>>(Documents.Where(d => d.Number > afterNumber && d.Number <= throughNumber).Take(size).ToArray());
        }
        public Task<IReadOnlyList<SearchTicket>> HydrateAsync(Actor actor, TicketSearchQuery query, IReadOnlyList<Guid> ids, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Index : ITicketSearchIndex
    {
        public Dictionary<Guid, PublicTicketDocument> Documents { get; } = [];
        public int Batches { get; private set; }
        public bool Fail { get; init; }
        public Task EnsureIndexAsync(CancellationToken ct) => Task.CompletedTask;
        public Task UpsertAsync(IReadOnlyList<PublicTicketDocument> documents, CancellationToken ct)
        {
            if (Fail) throw new RuleException(ErrorKind.Unavailable, "Search unavailable");
            Assert.InRange(documents.Count, 1, 100);
            foreach (var document in documents) Documents[document.Id] = document;
            Batches++;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<Guid>> SearchAsync(Actor actor, TicketSearchQuery query, CancellationToken ct) => throw new NotSupportedException();
    }
}
