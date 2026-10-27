using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record BodyCollisionPackage : SubsystemBodyPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.HandleAxisCollision;

    [Key(0)] public int CreatureId { get; init; }

    [Key(1)] public int TargetCreatureId { get; init; }

    [Key(2)] public Vector3 Impulse { get; init; }
}
