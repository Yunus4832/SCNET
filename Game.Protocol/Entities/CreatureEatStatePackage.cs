using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreatureEatStatePackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.EatPickable;

    [Key(1)] public bool IsActive { get; init; }
}
