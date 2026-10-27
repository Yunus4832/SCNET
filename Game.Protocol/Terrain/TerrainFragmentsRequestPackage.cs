using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public sealed record TerrainFragmentsRequestPackage : SubsystemTerrainPackage
{
    [IgnoreMember] public override DataType Type => DataType.RequestTerrainChunkFragments;

    [Key(0)] public IReadOnlyList<TerrainChunkFragmentRequest> FragmentRequests { get; init; } = [];
}
