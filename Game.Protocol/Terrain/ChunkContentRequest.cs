using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public readonly record struct ChunkContentRequest(
    [property: Key(0)] ChunkAllocationId Allocation,
    [property: Key(1)] long KnownContentVersion = 0
);
