using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public sealed record TerrainChunksRequestPackage : SubsystemTerrainPackage
{
    public const int MaximumRequests = 64;

    [IgnoreMember] public override DataType Type => DataType.RequestSyncChunks;

    [Key(0), MaxLength(MaximumRequests)] public IReadOnlyList<ChunkContentRequest> ChunkRequests { get; init; } = [];
}
