using System.Collections.ObjectModel;

using Engine.Core.Mathematics;

using Game.Protocol.Terrain;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record FurnitureDesignAddRequestPackage : FurniturePackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.RequestAdd;

    [Key(0)] public int FurnitureIndex { get; init; }

    [Key(1)] public string DesignXml { get; init; } = string.Empty;

    [Key(2)] public IReadOnlyDictionary<Point3, int> PointDict { get; init; } = ReadOnlyDictionary<Point3, int>.Empty;

    [Key(3)] public CellFace CellFace { get; init; }

    [Key(4)] public int StartValue { get; init; }
}
