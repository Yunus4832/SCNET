using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreatureShapeshiftStatePackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.Shapeshift;

    [Key(1)] public double StartTime { get; init; }
}
