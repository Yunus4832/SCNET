using EntitySystem.Core;

using Game.Commands;
using Game.Network.Transport;
using Game.Teams;

namespace Game.Network.Handling;

/// <summary>Only server-authored snapshots and prompts enter through this message.</summary>
public sealed class TeamManagePackageHandler : PackageHandlerBase<TeamManagePackage>
{
    public override void Handle(TeamManagePackage package, PackageReceiveContext context)
    {
        if (context.IsServer || context.Node is not { } node || GameManager.Project is not { } project) { return; }

        switch (package.Command)
        {
            case TeamManagePackage.CommandType.PromptJoinRequest:
            case TeamManagePackage.CommandType.PromptInvitation:
                if (node.Self?.PlayerAddress == package.Responder) { ShowPrompt(package, project); }

                break;
            case TeamManagePackage.CommandType.SyncTeams:
                project.FindSubsystem<SubsystemTeams>(true)!.Store.Replace(
                    package.Teams.Select(team =>
                        new Team(team.TeamId, team.Name, team.Leader, team.Members)
                    )
                );
                project.FindSubsystem<SubsystemCharacters>(true)!.Characters
                    .Find(character => character.IsLocalCharacter)
                    ?.GameWidget.RefreshPlayerViews();
                break;
        }
    }

    internal static void ShowPrompt(TeamManagePackage package, Project project)
    {
        var team = project.FindSubsystem<SubsystemTeams>(true)!.Store.Find(package.TeamId);
        if (team is null || CommonLib.Net.Self is not { } session || session.PlayerAddress != package.Responder)
        {
            return;
        }

        var name = PlayerSessionQuery.GetDisplayName(project, package.Initiator);
        var text = package.Command == TeamManagePackage.CommandType.PromptJoinRequest
            ? CommandText.Get("TeamJoinPrompt_Message", "{0} 申请加入你的队伍，是否同意？", name)
            : CommandText.Get("TeamInvitationPrompt_Message", "{0} 邀请你加入队伍“{1}”，是否同意？", name, team.Name);
        DialogsManager.Confirm(
            text,
            button => CommandGateway.SubmitSession(
                session,
                project,
                new RespondTeamRequestCommand(package.OperationId, button == MessageDialogButton.Button1)
            ),
            session.CharacterData?.GameWidget.GuiWidget
        );
    }
}
