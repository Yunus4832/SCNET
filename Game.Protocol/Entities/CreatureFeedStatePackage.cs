using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreatureFeedStatePackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.RandomFeed;

    [Key(1)] public bool IsActive { get; init; }
}
