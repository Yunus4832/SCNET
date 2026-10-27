using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ClientListPackage : ClientPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.SyncList;

    [Key(0), MaxLength(255)] public IReadOnlyList<ClientIdentity> List { get; init; } = [];
}
