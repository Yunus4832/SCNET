using Engine.Core;

namespace Game.Protocol.Blocks;

public sealed record MovingBlockPackage : IProtocolMessage
{
    public enum EventType
    {
        Add,
        Remove,
        Stopped,
        Update,
        PistonSound
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
