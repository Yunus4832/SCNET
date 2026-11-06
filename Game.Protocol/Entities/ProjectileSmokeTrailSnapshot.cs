using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ProjectileSmokeTrailSnapshot
{
    [Key(0)] public float Size { get; init; }

    [Key(1)] public double EndTime { get; init; }

    [Key(2)] public Color Color { get; init; }
}
