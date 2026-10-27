using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ConnectionRejectPackage([property: Key(0)] string Reason) : IProtocolMessage
{
}
