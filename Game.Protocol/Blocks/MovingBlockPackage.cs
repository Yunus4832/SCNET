using Engine.Core.Mathematics;

namespace Game.Protocol.Blocks;

public sealed record MovingBlockPackage : IProtocolMessage
{
    public enum EventType
    {
        Add = 0,

        Remove = 1,

        Stopped = 2,

        Update = 3,

        PistonSound = 4
    }

    public byte[]? AddData { get; init; }

    public EventType Type { get; init; }

    public int NetworkId { get; init; }

    public uint StateTick { get; init; }

    public Vector3 Position { get; init; }

    public Vector3 Velocity { get; init; }

    public float Speed { get; init; }

    public byte ID => (byte)PackageType.MovingBlockSet;
}
