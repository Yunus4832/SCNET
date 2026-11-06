using EntitySystem.Core;

using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Network.Transport;

namespace Game.Network.Handling.Presentation;

public sealed class ClientTeamManagePackageHandler : TeamManagePackageHandler
{
    public override void Handle(TeamManagePackage package, PackageReceiveContext context)
    {
        base.Handle(package, context);
        if (!IsAuthorityReply(context) || !context.Runtime.HasPresentation || context.Project is not { } project)
        {
            return;
        }

        switch (package)
        {
            case TeamPromptPackage prompt when context.Node!.Self?.PlayerAddress == prompt.Responder:
                ShowPrompt(prompt, project);
                break;
            case TeamSnapshotPackage:
                if (project.FindSubsystem<SubsystemCharacters>(true)!.Characters.Find(character =>
                        character.IsLocalCharacter
                    ) is { } local)
                {
                    project.FindSubsystem<SubsystemCharacterPresentation>(true)!.Get(local).GameWidget
                        .RefreshPlayerViews();
                }

                break;
        }
    }

    internal static void ShowPrompt(TeamPromptPackage package, Project project)
    {
        if (!WorldRuntimeContext.For(project).HasPresentation) { return; }

        var team = project.FindSubsystem<SubsystemTeams>(true)!.Store.Find(package.TeamId);
        if (team is null ||
            WorldConnections.For(project).Self is not { } session ||
            session.PlayerAddress != package.Responder)
        {
            return;
        }

        var name = Players.PlayerDisplayNameResolver.GetDisplayName(project, package.Initiator);
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
            Players.WorldConnectionResolver.FindCharacter(session) is { } character
                ? project.FindSubsystem<SubsystemCharacterPresentation>(true)!.Get(character).GameWidget.GuiWidget
                : null
        );
    }
}
