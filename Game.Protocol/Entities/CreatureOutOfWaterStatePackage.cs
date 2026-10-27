using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreatureOutOfWaterStatePackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.FishOutOfWater;

    [Key(1)] public bool IsActive { get; init; }
}
