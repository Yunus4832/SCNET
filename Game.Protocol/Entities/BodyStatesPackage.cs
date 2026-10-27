using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record BodyStatesPackage : SubsystemBodyPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.BodyUpdate;

    [Key(0)] public uint StateTick { get; init; }

    [Key(1)] public IReadOnlyList<BodyItem> BodyList { get; init; } = [];
}
