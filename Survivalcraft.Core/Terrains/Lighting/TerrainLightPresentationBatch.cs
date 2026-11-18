namespace Game.Terrains.Lighting;

/// <summary>Keeps related replica light updates together until presentation or invalidation.</summary>
public sealed class TerrainLightPresentationBatch(IReadOnlyList<ChunkAllocationId> allocations)
{
    private int _state;

    public IReadOnlyList<ChunkAllocationId> Allocations { get; } = allocations;

    public bool IsPending => Volatile.Read(ref _state) == 1;

    internal void Activate() => Interlocked.CompareExchange(ref _state, 1, 0);

    public void Release() => Volatile.Write(ref _state, 2);
}
