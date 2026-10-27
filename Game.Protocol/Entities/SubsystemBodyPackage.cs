using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(BodyCollisionPackage))]
[Union(1, typeof(BodyImpulsePackage))]
[Union(2, typeof(BodyStatesPackage))]
public abstract record SubsystemBodyPackage : IProtocolMessage
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

    [MessagePackObject]
    public record struct BodyItem
    {
        [Key(0)] public int CreatureId;

        [Key(1)] public Vector3 Position;

        [Key(2)] public Vector3 Velocity;

        [Key(3)] public Quaternion Rotation;

        [Key(4)] public Vector2 LookAngles;

        [Key(5)] public Vector3? FlyOrder;

        [Key(6)] public ChangeFlag ChangeFlag;
    }

    [IgnoreMember] public abstract EventType PackageEventType { get; }
}
