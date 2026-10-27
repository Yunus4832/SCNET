using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ClientAddedPackage : ClientPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.Add;

    [Key(0)] public ClientIdentity? Client { get; init; }
}
