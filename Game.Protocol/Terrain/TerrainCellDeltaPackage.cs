using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public sealed record TerrainCellDeltaPackage : SubsystemTerrainPackage
{
    [IgnoreMember] public override DataType Type => DataType.SyncTerrainCellDelta;

    [Key(0)] public TerrainCellDelta CellDelta { get; init; }
}
