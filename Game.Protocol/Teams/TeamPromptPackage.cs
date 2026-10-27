using Game.Protocol.Identity;

using MessagePack;

namespace Game.Protocol.Teams;

[MessagePackObject]
[Union(0, typeof(TeamJoinPromptPackage))]
[Union(1, typeof(TeamInvitationPromptPackage))]
public abstract record TeamPromptPackage : TeamManagePackage
{
    [Key(0)] public PlayerAddress Initiator { get; init; }

    [Key(1)] public PlayerAddress Responder { get; init; }

    [Key(2)] public Guid TeamId { get; init; }

    [Key(3)] public Guid OperationId { get; init; }
}
