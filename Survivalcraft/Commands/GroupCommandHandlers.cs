using Game.Network;
using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Game.Commands;

internal static class GroupCommandHandlers
{
    public static CommandResult CreateTeam(
        CommandContext context,
        CreateTeamCommand command)
    {
        if (!TryGetActor(context, out var actor, out var players, out var failure) ||
            !TryStart(players, actor, out failure))
        {
            return failure;
        }

        var changed = players.TryCreateGroup(actor, command.Name, out var message);
        if (changed)
        {
            BroadcastSnapshot(players);
            return LocalizedOk("team.created", message);
        }

        return LocalizedFail("team.create_failed", message);
    }

    public static CommandResult RequestJoin(
        CommandContext context,
        RequestJoinTeamCommand command)
    {
        if (!TryGetActor(context, out var actor, out var players, out var failure) ||
            !TryStart(players, actor, out failure))
        {
            return failure;
        }

        var created = players.TryCreateJoinRequest(
            actor,
            command.TeamId,
            out var operation,
            out var responder,
            out var message);
        if (!created)
        {
            return LocalizedFail("team.join_request_failed", message);
        }

        SendPrompt(operation!, responder!);
        return LocalizedPending("team.join_request_pending", message);
    }

    public static CommandResult InvitePlayer(
        CommandContext context,
        InvitePlayerToTeamCommand command)
    {
        if (!TryGetActor(context, out var actor, out var players, out var failure) ||
            !TryStart(players, actor, out failure))
        {
            return failure;
        }

        if (!Guid.TryParse(actor.GroupKey, out var teamId))
        {
            return CommandResult.LocalizedFail(
                "team.invalid_membership",
                "TeamRequired_Message",
                "你当前不在有效的队伍中。");
        }

        var created = players.TryCreateInvitation(
            actor,
            teamId,
            command.PlayerId,
            out var operation,
            out var responder,
            out var message);
        if (!created)
        {
            return LocalizedFail("team.invitation_failed", message);
        }

        SendPrompt(operation!, responder!);
        return LocalizedPending("team.invitation_pending", message);
    }

    public static CommandResult Respond(
        CommandContext context,
        RespondTeamRequestCommand command)
    {
        if (!TryGetActor(context, out var actor, out var players, out var failure))
        {
            return failure;
        }

        var changed = players.TryRespondToGroupOperation(
            actor,
            command.OperationId,
            command.Accepted,
            out var operation,
            out var message);
        if (operation is not null)
        {
            NotifyInitiator(
                players,
                actor,
                operation,
                command.Accepted,
                changed,
                message);
        }

        if (!changed)
        {
            return LocalizedFail("team.response_failed", message);
        }

        if (command.Accepted)
        {
            BroadcastSnapshot(players);
        }

        return LocalizedOk(
            command.Accepted ? "team.request_accepted" : "team.request_rejected",
            message);
    }

    public static CommandResult LeaveTeam(
        CommandContext context,
        LeaveTeamCommand command)
    {
        if (!TryGetActor(context, out var actor, out var players, out var failure) ||
            !TryStart(players, actor, out failure))
        {
            return failure;
        }

        var changed = players.TryLeaveGroup(actor, out var message);
        if (changed)
        {
            BroadcastSnapshot(players);
            return LocalizedOk("team.left", message);
        }

        return LocalizedFail("team.leave_failed", message);
    }

    private static bool TryGetActor(
        CommandContext context,
        out PlayerData actor,
        out SubsystemPlayers players,
        out CommandResult failure)
    {
        if (context.Project is null ||
            context.Principal.Player is not { } player)
        {
            actor = null!;
            players = null!;
            failure = CommandResult.LocalizedFail(
                "team.player_required",
                "TeamOnlinePlayerRequired_Message",
                "组队操作需要在线玩家。");
            return false;
        }

        actor = player;
        players = context.Project.FindSubsystem<SubsystemPlayers>(true)!;
        if (!players.PlayersData.Contains(actor))
        {
            failure = CommandResult.LocalizedFail(
                "team.player_not_loaded",
                "PlayerNotLoaded_Message",
                "玩家尚未加载到当前世界。");
            return false;
        }

        failure = null!;
        return true;
    }

    private static bool TryStart(
        SubsystemPlayers players,
        PlayerData actor,
        out CommandResult failure)
    {
        if (players.TryStartGroupOperation(actor, out var error))
        {
            failure = null!;
            return true;
        }

        failure = LocalizedFail("team.rate_limited", error!);
        return false;
    }

    private static void SendPrompt(
        SubsystemPlayers.PendingGroupOperation operation,
        PlayerData responder)
    {
        var package = GroupManagePackage.CreatePrompt(operation);
        if (responder.IsMainPlayer)
        {
            GroupManagePackageHandler.ShowPrompt(package, responder);
        }
        else if (responder.Client is not null)
        {
            package.To = responder.Client;
            CommonLib.Net.QueuePackage(package);
        }
    }

    private static void NotifyInitiator(
        SubsystemPlayers players,
        PlayerData responder,
        SubsystemPlayers.PendingGroupOperation operation,
        bool accepted,
        bool changed,
        SubsystemPlayers.GroupOperationMessage message)
    {
        var initiator = players.FindPlayerData(
            player => player.PlayerGUID == operation.Initiator);
        if (initiator is null || initiator == responder)
        {
            return;
        }

        var result = changed
            ? accepted
                ? LocalizedOk("team.request_accepted", message)
                : CommandResult.LocalizedOk(
                    "team.request_rejected",
                    "TeamRequestDeclined_Message",
                    "对方拒绝了队伍请求。")
            : accepted
                ? CommandResult.LocalizedFail(
                    "team.response_failed",
                    "TeamRequestFailed_Message",
                    "队伍请求处理失败：{0}",
                    FormatFallback(message))
                : LocalizedFail("team.response_failed", message);
        if (initiator.IsMainPlayer)
        {
            CommandResultPublisher.DisplayLocal(initiator.Project, result);
            initiator.GameWidget.RefreshPlayerViews();
        }
        else if (initiator.Client is not null)
        {
            var resultPackage = CommandPackage.CreateResult(
                result,
                operation.OperationId.ToString("N"));
            resultPackage.To = initiator.Client;
            CommonLib.Net.QueuePackage(resultPackage);
        }
    }

    private static void BroadcastSnapshot(SubsystemPlayers players)
    {
        CommonLib.Net.QueuePackage(GroupManagePackage.CreateSnapshot(players));
        players.PlayersData
            .Find(player => player.IsMainPlayer)?
            .GameWidget.RefreshPlayerViews();
    }

    private static CommandResult LocalizedOk(
        string code,
        SubsystemPlayers.GroupOperationMessage message)
    {
        return CommandResult.LocalizedOk(
            code,
            message.Key,
            message.Fallback,
            message.Arguments.ToArray());
    }

    private static CommandResult LocalizedPending(
        string code,
        SubsystemPlayers.GroupOperationMessage message)
    {
        return CommandResult.LocalizedPending(
            code,
            message.Key,
            message.Fallback,
            message.Arguments.ToArray());
    }

    private static CommandResult LocalizedFail(
        string code,
        SubsystemPlayers.GroupOperationMessage message)
    {
        return CommandResult.LocalizedFail(
            code,
            message.Key,
            message.Fallback,
            message.Arguments.ToArray());
    }

    private static string FormatFallback(
        SubsystemPlayers.GroupOperationMessage message)
    {
        return message.Arguments.Count == 0
            ? message.Fallback
            : string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                message.Fallback,
                message.Arguments.ToArray());
    }
}
