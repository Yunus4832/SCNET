using Game.Protocol.Identity;

using MessagePack;

namespace Game.Protocol.Teams;

[MessagePackObject]
[Union(0, typeof(TeamInvitationPromptPackage))]
[Union(1, typeof(TeamJoinPromptPackage))]
[Union(2, typeof(TeamSnapshotPackage))]
public abstract record TeamManagePackage : IProtocolMessage
{
    public enum CommandType : byte
    {
        PromptJoinRequest = 0,

        PromptInvitation = 1,

        SyncTeams = 2
    }

    [MessagePackObject]
    public sealed record TeamState
    {
        [Key(0)] public Guid TeamId { get; init; }

        [Key(1)] public string Name { get; init; } = string.Empty;

        [Key(2)] public PlayerAddress Leader { get; init; }

        [Key(3)] public IReadOnlyList<PlayerAddress> Members { get; init; } = [];
    }

    [IgnoreMember] public abstract CommandType Command { get; }
}
