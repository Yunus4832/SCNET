using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreatureChaseStatePackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.ChaseBehavior;

    [Key(1)] public bool IsActive { get; init; }
}
