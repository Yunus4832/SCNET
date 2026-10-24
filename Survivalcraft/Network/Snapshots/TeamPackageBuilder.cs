using Game.Teams;

namespace Game.Network.Snapshots;

public static class TeamPackageBuilder
{
    public static TeamManagePackage CreatePrompt(TeamOperation operation) => new()
    {
        Command = operation.Kind == TeamOperationKind.JoinRequest
            ? TeamManagePackage.CommandType.PromptJoinRequest
            : TeamManagePackage.CommandType.PromptInvitation,
        OperationId = operation.Id,
        Initiator = operation.Initiator,
        Responder = operation.Responder,
        TeamId = operation.TeamId
    };

    public static TeamManagePackage CreateSnapshot(SubsystemTeams teams) => new()
    {
        Command = TeamManagePackage.CommandType.SyncTeams,
        Teams = teams.Store.Teams.Values.Select(team => new TeamManagePackage.TeamState
            {
                TeamId = team.Id, Name = team.Name, Leader = team.Leader, Members = team.Members.ToArray()
            }
        ).ToArray()
    };
}
