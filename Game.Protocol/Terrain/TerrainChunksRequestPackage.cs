using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public sealed record TerrainChunksRequestPackage : SubsystemTerrainPackage
{
    [IgnoreMember] public override DataType Type => DataType.RequestSyncChunks;

    [Key(0)] public IReadOnlyList<ChunkContentRequest> ChunkRequests { get; init; } = [];
}
