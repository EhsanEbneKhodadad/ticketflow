namespace TicketFlow.Application.Search;

public sealed class SearchProjectionWorkflow(ISearchTicketStore store, ITicketSearchIndex index)
{
    public async Task ReconcileAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await index.EnsureIndexAsync(ct);
        var through = await store.LastNumberAsync(ct);
        long after = 0;
        while (after < through)
        {
            ct.ThrowIfCancellationRequested();
            var batch = await store.ReadBatchAsync(after, through, 100, ct);
            if (batch.Count == 0) break;
            await index.UpsertAsync(batch, ct);
            after = batch[^1].Number;
        }
    }
}
