using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ClientRemovedPackage : ClientPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.Remove;

    [Key(0)] public byte ClientId { get; init; }
}
