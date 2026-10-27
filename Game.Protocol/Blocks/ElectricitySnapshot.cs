using System.Collections.ObjectModel;

using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Blocks;

[MessagePackObject]
public sealed record ElectricitySnapshot
{
    [Key(0)] public int StartStep { get; init; }

    [Key(1)] public bool IsBaseline { get; init; }

    [Key(2)]
    public IReadOnlyDictionary<Point3, float> SaveData { get; init; } = ReadOnlyDictionary<Point3, float>.Empty;

    [Key(3)] public IReadOnlyList<Point3> Removed { get; init; } = [];
}
