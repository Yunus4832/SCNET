using Game.Protocol.Identity;

namespace Game.Protocol.Teams;

public sealed record TeamManagePackage : IProtocolMessage
{
    public enum CommandType : byte
    {
        PromptJoinRequest = 0,

        PromptInvitation = 1,

        SyncTeams = 2
    }

    public sealed record TeamState
    {
        public Guid TeamId { get; init; }

        public string Name { get; init; } = string.Empty;

        public PlayerAddress Leader { get; init; }

        public IReadOnlyList<PlayerAddress> Members { get; init; } = [];
    }

    public CommandType Command { get; init; }

    public PlayerAddress Initiator { get; init; }

    public PlayerAddress Responder { get; init; }

    public Guid TeamId { get; init; }

    public Guid OperationId { get; init; }

    public IReadOnlyList<TeamState> Teams { get; init; } = [];

    public byte ID => (byte)PackageType.TeamManage;
}
