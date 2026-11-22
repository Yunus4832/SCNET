namespace Game.Terrains.Distribution;

/// <summary>为已解码快照持有一个槽位，直到快照提交或队列中的消息被释放。</summary>
public sealed class ClientSnapshotReservation : IDisposable
{
    internal ClientSnapshotReservation(
        ClientChunkContentTransport owner,
        ChunkAllocationId allocation,
        Action? abandoned
    )
    {
        Owner = owner;
        Allocation = allocation;
        _abandoned = abandoned;
    }

    private int _released;

    private Action? _abandoned;

    internal ClientChunkContentTransport Owner { get; }

    internal ChunkAllocationId Allocation { get; }

    internal bool TryConsume()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0) { return false; }

        Interlocked.Exchange(ref _abandoned, null);
        return true;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0) { return; }

        Owner.ReleaseReservation();
        Interlocked.Exchange(ref _abandoned, null)?.Invoke();
    }
}
