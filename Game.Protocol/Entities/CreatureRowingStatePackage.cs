using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record CreatureRowingStatePackage : ComponentBehaviorPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.HumanRow;

    [Key(1)] public bool RowLeft { get; init; }

    [Key(2)] public bool RowRight { get; init; }
}
