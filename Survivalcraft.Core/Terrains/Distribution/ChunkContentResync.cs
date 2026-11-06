namespace Game.Terrains.Distribution;

/// <summary>A version floor whose missing deltas must be recovered by a complete snapshot.</summary>
public readonly record struct ChunkContentResync(ChunkAllocationId Allocation, long RequiredContentVersion);
