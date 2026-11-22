namespace Game.Terrains.Lighting;

/// <summary>让副本的关联光照更新保持成组，直到呈现或失效。</summary>
public sealed class TerrainLightPresentationBatch(IReadOnlyList<ChunkAllocationId> allocations)
{
    public readonly record struct Source(
        ChunkAllocationId Allocation,
        long Sequence,
        ushort Sections,
        TerrainLightBoundaryChanges? Boundaries = null
    );

    private int _state;

    public IReadOnlyList<ChunkAllocationId> Allocations { get; } = allocations;

    public bool IsPending => Volatile.Read(ref _state) == 1;

    internal void Activate() => Interlocked.CompareExchange(ref _state, 1, 0);

    public void Release() => Volatile.Write(ref _state, 2);
}
