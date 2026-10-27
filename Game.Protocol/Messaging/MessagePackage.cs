using MessagePack;

namespace Game.Protocol.Messaging;

[MessagePackObject]
public sealed record MessagePackage([property: Key(0)] GameMessage GameMessage) : IProtocolMessage
{
}
