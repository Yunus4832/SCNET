using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ConnectionPhaseAckPackage(
    [property: Key(0)] Guid Epoch,
    [property: Key(1)] ConnectionPhase Phase
) : IProtocolMessage
{
}
