namespace Game.Protocol.Connection;

public sealed record ConnectionPhaseAckPackage(Guid Epoch, ConnectionPhase Phase) : IProtocolMessage
{
    public byte ID => (byte)PackageType.ConnectionPhaseAck;
}
