namespace Game.TerrainSerializers;

/// <summary>为一次区块分配的卸载回调预留的单次保存容量。</summary>
public sealed class TerrainUnloadReservation : IDisposable
{
    private TerrainSaveCoordinator? _owner;

    private TerrainChunk? _chunk;

    private readonly ulong _generation;

    internal Point2 Coords { get; }

    internal TerrainUnloadReservation(TerrainSaveCoordinator owner, TerrainChunk chunk)
    {
        _owner = owner;
        _chunk = chunk;
        _generation = chunk.AllocationGeneration;
        Coords = chunk.Coords;
    }

    /// <summary>回调完成后捕获最终的脏数据，即使失败也会消耗本次预留。</summary>
    public bool Commit(TerrainChunk chunk)
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        if (owner == null) { return false; }

        try { return owner.CommitUnload(this, chunk); }
        finally { Volatile.Write(ref _chunk, null); }
    }

    public void Dispose()
    {
        var owner = Interlocked.Exchange(ref _owner, null);
        if (owner == null) { return; }

        try { owner.ReleaseUnload(this); }
        finally { Volatile.Write(ref _chunk, null); }
    }

    internal bool Matches(TerrainChunk chunk)
    {
        return ReferenceEquals(_chunk, chunk) && chunk.Coords == Coords && chunk.AllocationGeneration == _generation;
    }

    internal void Revoke(TerrainSaveCoordinator owner)
    {
        if (ReferenceEquals(Interlocked.CompareExchange(ref _owner, null, owner), owner))
        {
            Volatile.Write(ref _chunk, null);
        }
    }
}
