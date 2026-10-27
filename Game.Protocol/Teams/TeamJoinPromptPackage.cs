using MessagePack;

namespace Game.Protocol.Teams;

[MessagePackObject]
public sealed record TeamJoinPromptPackage : TeamPromptPackage
{
    [IgnoreMember] public override CommandType Command => CommandType.PromptJoinRequest;
}
