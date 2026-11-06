using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ClientRemovedPackage : ClientPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.Remove;

    [Key(0)] public byte ClientId { get; init; }

    [Key(1)] public Guid ConnectionEpoch { get; init; }

    [Key(2)] public long ConnectionGeneration { get; init; }
}
