using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreatureWhalePlumePackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.WhalePlume;

    [Key(1)] public bool IsActive { get; init; }

    [Key(2)] public float Size { get; init; }

    [Key(3)] public float Duration { get; init; }
}
