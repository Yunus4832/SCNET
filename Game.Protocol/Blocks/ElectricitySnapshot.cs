using System.Collections.ObjectModel;

using Engine.Core;

namespace Game.Protocol.Blocks;

public sealed record ElectricitySnapshot
{
    public int StartStep { get; init; }

    public bool IsBaseline { get; init; }

    public IReadOnlyDictionary<Point3, float> SaveData { get; init; } = ReadOnlyDictionary<Point3, float>.Empty;

    public IReadOnlyList<Point3> Removed { get; init; } = [];
}
