using Engine.Core;

namespace Game.Protocol.Entities;

public sealed record SubsystemBodyPackage : IProtocolMessage
{
    [Flags]
    public enum ChangeFlag : byte
    {
        None = 0,

        LookAnglesChange = 1,

        FlyOrderChange = 2,

        PositionChange = 4,

        RotationChange = 8,

        VelocityChange = 16
    }

    public enum EventType
    {
        BodyUpdate = 0,

        ApplyImpulse = 1,

        HandleAxisCollision = 2
    }

    public IReadOnlyList<BodyItem> BodyList { get; init; } = [];

    public int CreatureId { get; init; }

    public EventType PackageEventType { get; init; }

    public Vector3 Impulse { get; init; }

    public int TargetCreatureId { get; init; }

    public uint StateTick { get; init; }

    public byte ID => (byte)PackageType.SubsystemBody;

    public record struct BodyItem
    {
        public int CreatureId;

        public Vector3 Position;

        public Vector3 Velocity;

        public Quaternion Rotation;

        public Vector2 LookAngles;

        public Vector3? FlyOrder;

        public ChangeFlag ChangeFlag;
    }
}
