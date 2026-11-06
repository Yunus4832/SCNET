using Game.Teams;

namespace Game.Network.Handling;

public class TeamManagePackageHandler : PackageHandlerBase<TeamManagePackage>
{
    protected static bool IsAuthorityReply(PackageReceiveContext context) =>
        !context.IsServer &&
        !context.Runtime.IsClosed &&
        context.Node?.Server is { } authority &&
        ReferenceEquals(context.Sender, authority) &&
        ReferenceEquals(authority.World, context.Runtime);

    public override void Handle(TeamManagePackage package, PackageReceiveContext context)
    {
        if (!IsAuthorityReply(context) || context.Project is not { } project) { return; }

        if (package is TeamSnapshotPackage snapshot)
        {
            project.FindSubsystem<SubsystemTeams>(true)!.Store.Replace(
                snapshot.Teams.Select(team => new Team(team.TeamId, team.Name, team.Leader, team.Members))
            );
        }
    }
}
