using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ClientStateChangedPackage : ClientPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.StateChange;

    [Key(0)] public byte ClientId { get; init; }

    [Key(1)] public ClientState State { get; init; }

    [Key(2)] public Guid ConnectionEpoch { get; init; }

    [Key(3)] public long ConnectionGeneration { get; init; }
}
