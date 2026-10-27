using MessagePack;

namespace Game.Protocol.Teams;

[MessagePackObject]
public sealed record TeamInvitationPromptPackage : TeamPromptPackage
{
    [IgnoreMember] public override CommandType Command => CommandType.PromptInvitation;
}
