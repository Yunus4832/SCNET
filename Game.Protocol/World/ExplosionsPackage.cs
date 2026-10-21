using System.Collections.ObjectModel;

using Engine.Core;

namespace Game.Protocol.World;

public sealed record ExplosionsPackage : IProtocolMessage
{
    public enum EventType { Sound = 0, Cell = 1 }

    public IReadOnlyDictionary<Point2, IReadOnlyList<ExplosionCell>> Cells { get; init; } =
        ReadOnlyDictionary<Point2, IReadOnlyList<ExplosionCell>>.Empty;

    public float Delay { get; init; }

    public float Level { get; init; }

    public Vector3 Position { get; init; }

    public EventType Type { get; init; }

    public byte ID => (byte)PackageType.Explosion;

    public ExplosionsPackage()
    {
    }

    public ExplosionsPackage(Vector3 position, float level, float delay)
    {
        Position = position;
        Level = level;
        Delay = delay;
        Type = EventType.Sound;
    }
}
