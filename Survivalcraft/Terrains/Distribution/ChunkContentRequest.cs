namespace Game.Terrains.Distribution;

public readonly record struct ChunkContentRequest(ChunkAllocationId Allocation, long KnownContentVersion = 0);
