namespace Game.Protocol.Connection;

public sealed record ConnectionRequestPackage(
    Guid TmpToken,
    int ProtocolVersion,
    Guid MultiplayerClientId,
    string ModDataHash,
    int Magic = GameProtocol.ConnectionMagic
) : IProtocolMessage
{
    public byte ID => (byte)PackageType.ConnectionRequest;
}
