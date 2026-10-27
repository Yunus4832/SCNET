using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public sealed record TerrainCellEditCompletedPackage : SubsystemTerrainPackage
{
    [IgnoreMember] public override DataType Type => DataType.CellEditCompleted;

    [Key(0)] public int EditRequestId { get; init; }

    [Key(1)] public Point3 EditedCell { get; init; }

    [Key(2)] public long EditContentVersion { get; init; }
}
