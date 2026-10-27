using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreatureDigStatePackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.DigInMud;

    [Key(1)] public bool IsActive { get; init; }
}
