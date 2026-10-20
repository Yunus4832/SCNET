namespace Game.Protocol.Players;

public sealed record GroupManagePackage : IProtocolMessage
{
    public enum CommandType : byte
    {
        PromptJoinRequest,
        PromptInvitation,
        SyncGroups
    }
    public sealed record GroupState
    {
        public Guid GroupKey { get; init; }
        public string Name { get; init; } = string.Empty;
        public IReadOnlyList<Guid> Members { get; init; } = [];
    }
    public CommandType Command { get; init; }
    public Guid FromPlayer { get; init; }
    public Guid ToPlayer { get; init; }
    public Guid GroupKey { get; init; }
    public Guid OperationId { get; init; }
    public IReadOnlyList<GroupState> Groups { get; init; } = [];
    public byte ID => (byte)PackageType.GroupManage;
}
