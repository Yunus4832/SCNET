using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ProjectileAddedPackage : ProjectilePackage
{
    [IgnoreMember] public override EventType Type => EventType.Add;

    [Key(1)] public uint StateTick { get; init; }

    [Key(2)] public Vector3 Position { get; init; }

    [Key(3)] public Vector3 Rotation { get; init; }

    [Key(4)] public Vector3 Velocity { get; init; }

    [Key(5)] public Vector3 AngularVelocity { get; init; }

    [Key(6)] public int Value { get; init; }

    [Key(7)] public Vector3 TrailOffset { get; init; }

    [Key(8)] public int OwnerId { get; init; }

    [Key(9)] public bool IsFireProjectile { get; init; }
}
