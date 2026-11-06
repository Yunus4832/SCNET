using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public sealed record TerrainCellDeltaPackage : SubsystemTerrainPackage
{
    [IgnoreMember] public override DataType Type => DataType.SyncTerrainCellDelta;

    public const int MaximumDeltas = 64;

    [Key(0)] public IReadOnlyList<TerrainCellDelta> CellDeltas { get; init; } = [];
}
