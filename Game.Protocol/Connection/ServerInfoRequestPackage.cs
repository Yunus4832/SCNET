using MessagePack;

namespace Game.Protocol.Connection;

[MessagePackObject]
public sealed record ServerInfoRequestPackage : ServerInfoPackage
{
    [IgnoreMember] public override bool RequestInfo => true;
}
