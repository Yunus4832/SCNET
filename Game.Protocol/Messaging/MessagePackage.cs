namespace Game.Protocol.Messaging;

public sealed record MessagePackage(GameMessage GameMessage) : IProtocolMessage
{
    public byte ID => (byte)PackageType.Message;
}
