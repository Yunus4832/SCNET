namespace Game.Protocol.Connection;

public sealed record ConnectionRequestPackage(
    Guid TmpToken,
    int ProtocolVersion,
    string ModDataHash,
    int Magic = GameProtocol.ConnectionMagic
) : IProtocolMessage
{
    public byte ID => (byte)PackageType.ConnectionRequest;
}
