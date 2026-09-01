namespace Game.Terrains.Distribution;

/// <summary>
///     Exchanges state between the main thread and terrain worker through two one-way mailboxes.
/// </summary>
public static class TerrainChunkStateExchange
{
    public static void RequestDowngrade(TerrainChunk chunk, TerrainChunkState state)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.MainThreadState > state)
        {
            chunk.MainThreadState = state;
        }

        chunk.QueueWorkerDowngrade(chunk.MainThreadState);
    }

    public static bool ReceiveOnMainThread(IEnumerable<TerrainChunk> chunks)
    {
        var hasPendingDowngrade = false;
        foreach (var chunk in chunks)
        {
            if (chunk.HasQueuedWorkerDowngrade)
            {
                // A worker publication computed before a main-thread downgrade is obsolete.
                chunk.DiscardPublishedWorkerState();
                hasPendingDowngrade = true;
            }
            else if (chunk.TryConsumePublishedWorkerState(out var state))
            {
                chunk.MainThreadState = state;
            }
        }

        return hasPendingDowngrade;
    }

    public static void ExchangeOnWorkerThread(IEnumerable<TerrainChunk> chunks)
    {
        foreach (var chunk in chunks)
        {
            if (chunk.TryConsumeWorkerDowngrade(out var state))
            {
                chunk.WorkerState = state;
                chunk.DiscardPublishedWorkerState();
            }
            else
            {
                chunk.PublishWorkerState(chunk.WorkerState);
            }
        }
    }
}
