using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record BodyImpulsePackage : SubsystemBodyPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.ApplyImpulse;

    [Key(0)] public int CreatureId { get; init; }

    [Key(1)] public Vector3 Impulse { get; init; }
}
