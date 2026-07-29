using Game.Commands;

namespace Game.Network.Packages.Handlers;

/// <summary>
/// Applies server-authored group state and interaction prompts on clients.
/// Group mutations are commands and are never accepted through this package.
/// </summary>
public sealed class GroupManagePackageHandler : PackageHandlerBase<GroupManagePackage>
{
    public override void Handle(GroupManagePackage package, NetNode? netNode, bool isServer)
    {
        if (isServer ||
            netNode is null ||
            GameManager.Project is not { } project)
        {
            return;
        }

        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        switch (package.Command)
        {
            case GroupManagePackage.CommandType.PromptJoinRequest:
            case GroupManagePackage.CommandType.PromptInvitation:
                var mainPlayer = subsystemPlayers.PlayersData.Find(player => player.IsMainPlayer);
                if (mainPlayer is not null && mainPlayer.PlayerGUID == package.ToPlayer)
                {
                    ShowPrompt(package, mainPlayer);
                }

                break;
            case GroupManagePackage.CommandType.SyncGroups:
                ApplyGroupSnapshot(subsystemPlayers, package);
                break;
        }
    }

    internal static void ShowPrompt(
        GroupManagePackage package,
        PlayerData responder)
    {
        var subsystemPlayers = responder.SubsystemPlayers;
        var initiator = subsystemPlayers.FindPlayerData(
            player => player.PlayerGUID == package.FromPlayer);
        subsystemPlayers.ServerGroups.TryGetValue(package.GroupKey.ToString(), out var group);
        if (initiator is null || group is null)
        {
            return;
        }

        var text = package.Command is GroupManagePackage.CommandType.PromptJoinRequest
            ? CommandText.Get(
                "TeamJoinPrompt_Message",
                "{0} 申请加入你的队伍，是否同意？",
                initiator.Name)
            : CommandText.Get(
                "TeamInvitationPrompt_Message",
                "{0} 邀请你加入队伍“{1}”，是否同意？",
                initiator.Name,
                group.Name);
        DialogsManager.Confirm(
            text,
            button => CommandGateway.Submit(
                responder,
                new RespondTeamRequestCommand(
                    package.OperationId,
                    button == MessageDialogButton.Button1)),
            responder.GameWidget.GuiWidget);
    }

    private static void ApplyGroupSnapshot(
        SubsystemPlayers subsystemPlayers,
        GroupManagePackage package)
    {
        subsystemPlayers.ServerGroups.Clear();
        foreach (var playerData in subsystemPlayers.PlayersData)
        {
            playerData.GroupKey = string.Empty;
        }

        foreach (var state in package.Groups)
        {
            var key = state.GroupKey.ToString();
            if (subsystemPlayers.ServerGroups.ContainsKey(key))
            {
                continue;
            }

            var group = new SubsystemPlayers.Group { Name = state.Name };
            foreach (var member in state.Members.Distinct())
            {
                if (subsystemPlayers.GetPlayerGroupKey(member).Length == 0)
                {
                    group.Members.Add(member);
                }
            }

            subsystemPlayers.ServerGroups.Add(key, group);
            foreach (var member in group.Members)
            {
                var playerData = subsystemPlayers.FindPlayerData(
                    player => player.PlayerGUID == member);
                if (playerData is not null)
                {
                    playerData.GroupKey = key;
                }
            }
        }

        subsystemPlayers.PlayersData
            .Find(player => player.IsMainPlayer)?
            .GameWidget.RefreshPlayerViews();
    }
}
