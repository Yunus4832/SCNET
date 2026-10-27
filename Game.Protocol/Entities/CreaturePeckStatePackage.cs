using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreaturePeckStatePackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.RandomPeck;

    [Key(1)] public bool IsActive { get; init; }
}
