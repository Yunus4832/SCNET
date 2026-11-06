using Game.Protocol.Localization;

using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ConnectionRejectPackage([property: Key(0)] LocalizedText Reason) : IProtocolMessage
{
    public ConnectionRejectPackage(string reason) : this(LocalizedText.Literal(reason))
    {
    }
}
