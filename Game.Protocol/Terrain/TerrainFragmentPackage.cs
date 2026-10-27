using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public sealed record TerrainFragmentPackage : SubsystemTerrainPackage
{
    [IgnoreMember] public override DataType Type => DataType.SyncTerrainChunkFragment;

    [Key(0)] public EncodedTerrainChunkFragment ChunkFragment { get; init; }
}
