namespace Game.Protocol.Connection;

public sealed record ConnectionRejectPackage(string Reason) : IProtocolMessage
{
    public byte ID => (byte)PackageType.ConnectionReject;
}
