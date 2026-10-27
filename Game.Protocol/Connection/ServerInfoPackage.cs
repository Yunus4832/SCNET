using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
[Union(0, typeof(ServerInfoRequestPackage))]
[Union(1, typeof(ServerInfoResponsePackage))]
public abstract record ServerInfoPackage : IProtocolMessage
{
    [IgnoreMember] public abstract bool RequestInfo { get; }
}
