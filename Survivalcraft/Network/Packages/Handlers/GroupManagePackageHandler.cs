using Game.Messaging;

namespace Game.Network.Packages.Handlers;

public sealed class GroupManagePackageHandler : PackageHandlerBase<GroupManagePackage>
{
    public override void Handle(GroupManagePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode is null || GameManager.Project is not { } project)
        {
            return;
        }

        var subsystemPlayers = project.FindSubsystem<SubsystemPlayers>(true)!;
        if (!isServer)
        {
            HandleClientPackage(package, subsystemPlayers, netNode);
            return;
        }

        if (package.From?.PlayerData is not { } actor ||
            !subsystemPlayers.PlayersData.Contains(actor))
        {
            return;
        }

        HandleServerRequest(package, actor, subsystemPlayers, netNode);
    }

    public static void HandleLocalRequest(
        GroupManagePackage package,
        PlayerData actor,
        NetNode netNode)
    {
        HandleServerRequest(package, actor, actor.SubsystemPlayers, netNode);
    }

    private static void HandleServerRequest(
        GroupManagePackage package,
        PlayerData actor,
        SubsystemPlayers subsystemPlayers,
        NetNode netNode)
    {
        if (package.Command is not GroupManagePackage.CommandType.RespondRequest &&
            !subsystemPlayers.TryStartGroupOperation(actor, out var rateError))
        {
            SendResult(actor, false, rateError, netNode);
            return;
        }

        switch (package.Command)
        {
            case GroupManagePackage.CommandType.CreateGroup:
            {
                var changed = subsystemPlayers.TryCreateGroup(
                    actor,
                    package.GroupName,
                    out var message);
                SendResult(actor, changed, message, netNode);
                if (changed)
                {
                    BroadcastSnapshot(subsystemPlayers, netNode);
                }

                break;
            }
            case GroupManagePackage.CommandType.RequestJoinGroup:
            {
                var created = subsystemPlayers.TryCreateJoinRequest(
                    actor,
                    package.GroupKey,
                    out var operation,
                    out var responder,
                    out var message);
                SendResult(actor, created, message, netNode);
                if (created)
                {
                    SendPrompt(operation!, responder!, subsystemPlayers, netNode);
                }

                break;
            }
            case GroupManagePackage.CommandType.InviteJoinGroup:
            {
                var created = subsystemPlayers.TryCreateInvitation(
                    actor,
                    package.GroupKey,
                    package.ToPlayer,
                    out var operation,
                    out var responder,
                    out var message);
                SendResult(actor, created, message, netNode);
                if (created)
                {
                    SendPrompt(operation!, responder!, subsystemPlayers, netNode);
                }

                break;
            }
            case GroupManagePackage.CommandType.RespondRequest:
            {
                var changed = subsystemPlayers.TryRespondToGroupOperation(
                    actor,
                    package.OperationId,
                    package.Result,
                    out var operation,
                    out var message);
                SendResult(actor, changed, message, netNode, package.OperationId);
                if (operation is not null)
                {
                    var initiator = subsystemPlayers.FindPlayerData(
                        player => player.PlayerGUID == operation.Initiator);
                    if (initiator is not null && initiator != actor)
                    {
                        var initiatorMessage = package.Result && changed
                            ? message
                            : package.Result
                                ? $"队伍请求处理失败：{message}"
                                : "对方拒绝了队伍请求。";
                        SendResult(
                            initiator,
                            changed,
                            initiatorMessage,
                            netNode,
                            package.OperationId);
                    }
                }

                if (package.Result && changed)
                {
                    BroadcastSnapshot(subsystemPlayers, netNode);
                }

                break;
            }
            case GroupManagePackage.CommandType.ExitGroup:
            {
                var changed = subsystemPlayers.TryLeaveGroup(actor, out var message);
                SendResult(actor, changed, message, netNode);
                if (changed)
                {
                    BroadcastSnapshot(subsystemPlayers, netNode);
                }

                break;
            }
        }
    }

    private static void SendPrompt(
        SubsystemPlayers.PendingGroupOperation operation,
        PlayerData responder,
        SubsystemPlayers subsystemPlayers,
        NetNode netNode)
    {
        var package = new GroupManagePackage
        {
            Command = operation.Kind is SubsystemPlayers.PendingGroupOperationKind.JoinRequest
                ? GroupManagePackage.CommandType.RequestJoinGroup
                : GroupManagePackage.CommandType.InviteJoinGroup,
            OperationId = operation.OperationId,
            FromPlayer = operation.Initiator,
            ToPlayer = operation.Responder,
            GroupKey = operation.GroupKey
        };
        if (responder.IsMainPlayer)
        {
            ShowPrompt(package, responder, subsystemPlayers, netNode, true);
        }
        else if (responder.Client is not null)
        {
            package.To = responder.Client;
            netNode.QueuePackage(package);
        }
    }

    private static void HandleClientPackage(
        GroupManagePackage package,
        SubsystemPlayers subsystemPlayers,
        NetNode netNode)
    {
        switch (package.Command)
        {
            case GroupManagePackage.CommandType.RequestJoinGroup:
            case GroupManagePackage.CommandType.InviteJoinGroup:
                var mainPlayer = subsystemPlayers.PlayersData.Find(player => player.IsMainPlayer);
                if (mainPlayer is not null && mainPlayer.PlayerGUID == package.ToPlayer)
                {
                    ShowPrompt(package, mainPlayer, subsystemPlayers, netNode, false);
                }

                break;
            case GroupManagePackage.CommandType.SyncGroups:
                ApplyGroupSnapshot(subsystemPlayers, package);
                break;
            case GroupManagePackage.CommandType.OperationResult:
                DialogsManager.HideLoadingDialogs();
                var player = subsystemPlayers.PlayersData.Find(item => item.IsMainPlayer);
                if (player is not null)
                {
                    DisplayResult(player, package.Result, package.Message);
                    player.GameWidget.RefreshPlayerViews();
                }

                break;
        }
    }

    private static void ShowPrompt(
        GroupManagePackage package,
        PlayerData responder,
        SubsystemPlayers subsystemPlayers,
        NetNode netNode,
        bool localServer)
    {
        var initiator = subsystemPlayers.FindPlayerData(
            player => player.PlayerGUID == package.FromPlayer);
        subsystemPlayers.ServerGroups.TryGetValue(package.GroupKey.ToString(), out var group);
        if (initiator is null || group is null)
        {
            return;
        }

        var text = package.Command is GroupManagePackage.CommandType.RequestJoinGroup
            ? $"{initiator.Name} 申请加入你的队伍，是否同意？"
            : $"{initiator.Name} 邀请你加入队伍“{group.Name}”，是否同意？";
        DialogsManager.Confirm(
            text,
            button =>
            {
                var response = GroupManagePackage.CreateResponse(
                    package.OperationId,
                    button == MessageDialogButton.Button1);
                if (localServer)
                {
                    HandleServerRequest(response, responder, subsystemPlayers, netNode);
                }
                else
                {
                    netNode.QueuePackage(response);
                }
            },
            responder.GameWidget.GuiWidget);
    }

    private static void SendResult(
        PlayerData player,
        bool success,
        string message,
        NetNode netNode,
        Guid operationId = default)
    {
        if (player.IsMainPlayer)
        {
            DialogsManager.HideLoadingDialogs();
            DisplayResult(player, success, message);
            player.GameWidget.RefreshPlayerViews();
            return;
        }

        if (player.Client is not null)
        {
            var package = GroupManagePackage.CreateResult(
                success,
                message,
                operationId);
            package.To = player.Client;
            netNode.QueuePackage(package);
        }
    }

    private static void DisplayResult(PlayerData player, bool success, string message)
    {
        player.SubsystemGameWidgets.Messages.DisplayLocal(
            GameMessage.System(
                message,
                success ? GameMessageTone.Normal : GameMessageTone.Error,
                GameMessagePresentation.Toast));
    }

    private static void BroadcastSnapshot(SubsystemPlayers subsystemPlayers, NetNode netNode)
    {
        netNode.QueuePackage(GroupManagePackage.CreateSnapshot(subsystemPlayers));
        subsystemPlayers.PlayersData
            .Find(player => player.IsMainPlayer)?
            .GameWidget.RefreshPlayerViews();
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
