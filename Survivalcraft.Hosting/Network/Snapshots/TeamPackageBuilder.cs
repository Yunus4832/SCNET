using Game.Teams;

namespace Game.Network.Snapshots;

public static class TeamPackageBuilder
{
    public static TeamPromptPackage CreatePrompt(TeamOperation operation)
    {
        return operation.Kind == TeamOperationKind.JoinRequest
            ? new TeamJoinPromptPackage
            {
                OperationId = operation.Id, Initiator = operation.Initiator,
                Responder = operation.Responder, TeamId = operation.TeamId
            }
            : new TeamInvitationPromptPackage
            {
                OperationId = operation.Id, Initiator = operation.Initiator,
                Responder = operation.Responder, TeamId = operation.TeamId
            };
    }

    public static TeamManagePackage CreateSnapshot(SubsystemTeams teams) => new TeamSnapshotPackage
    {
        Teams = teams.Store.Teams.Values.Select(team => new TeamManagePackage.TeamState
            {
                TeamId = team.Id, Name = team.Name, Leader = team.Leader, Members = team.Members.ToArray()
            }
        ).ToArray()
    };
}
