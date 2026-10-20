namespace Game.Network.Snapshots;

public static class GroupPackageBuilder
{
    public static GroupManagePackage CreatePrompt(SubsystemPlayers.PendingGroupOperation operation) => new()
    {
        Command = operation.Kind == SubsystemPlayers.PendingGroupOperationKind.JoinRequest
        ? GroupManagePackage.CommandType.PromptJoinRequest : GroupManagePackage.CommandType.PromptInvitation,
        OperationId = operation.OperationId,
        FromPlayer = operation.Initiator,
        ToPlayer = operation.Responder,
        GroupKey = operation.GroupKey
    };
    public static GroupManagePackage CreateSnapshot(SubsystemPlayers players) => new()
    {
        Command = GroupManagePackage.CommandType.SyncGroups,
        Groups = players.ServerGroups.Select(item => new GroupManagePackage.GroupState
        { GroupKey = Guid.Parse(item.Key), Name = item.Value.Name, Members = item.Value.Members.ToArray() }).ToArray()
    };
}
