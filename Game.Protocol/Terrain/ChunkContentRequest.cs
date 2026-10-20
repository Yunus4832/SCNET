namespace Game.Protocol.Terrain;

public readonly record struct ChunkContentRequest(ChunkAllocationId Allocation, long KnownContentVersion = 0);
