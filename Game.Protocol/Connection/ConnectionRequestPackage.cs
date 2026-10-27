using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ConnectionRequestPackage(
    [property: Key(0)] Guid TmpToken,
    [property: Key(1)] int ProtocolVersion,
    [property: Key(2)] string ModDataHash,
    [property: Key(3)] int Magic = GameProtocol.ConnectionMagic
) : IProtocolMessage
{
}
