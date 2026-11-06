using Game.Teams;

namespace Game.Commands.BuiltIn;

internal static class TeamCommandHandlers
{
    public static CommandResult CreateTeam(CommandContext context, CreateTeamCommand command)
    {
        if (!TryGetActor(context, out var actor, out var teams, out var failure) ||
            !TryStart(teams, actor, out failure)) { return failure; }

        var changed = teams.TryCreate(actor, command.Name, out var message);
        if (changed) { Broadcast(context); }

        return Result(changed, changed ? "team.created" : "team.create_failed", message);
    }

    public static CommandResult LeaveTeam(CommandContext context, LeaveTeamCommand command)
    {
        if (!TryGetActor(context, out var actor, out var teams, out var failure) ||
            !TryStart(teams, actor, out failure)) { return failure; }

        var changed = teams.TryLeave(actor, out var message);
        if (changed) { Broadcast(context); }

        return Result(changed, changed ? "team.left" : "team.leave_failed", message);
    }

    public static CommandResult RequestJoin(CommandContext context, RequestJoinTeamCommand command)
    {
        if (!TryGetActor(context, out var actor, out var teams, out var failure) ||
            !TryStart(teams, actor, out failure)) { return failure; }

        var team = teams.Find(command.TeamId);
        if (team is not null && WorldConnectionQuery.Find(context.Project!, team.Leader) is null)
        {
            return CommandResult.LocalizedFail("team.leader_offline", "TeamLeaderOffline_Message", "队长当前不在线，无法处理加入申请。");
        }

        if (!teams.TryRequestJoin(actor, command.TeamId, out var operation, out var message))
        {
            return Result(false, "team.join_request_failed", message);
        }

        SendPrompt(context, operation!);
        return Pending("team.join_request_pending", message);
    }

    public static CommandResult InvitePlayer(CommandContext context, InvitePlayerToTeamCommand command)
    {
        if (!TryGetActor(context, out var actor, out var teams, out var failure) ||
            !TryStart(teams, actor, out failure)) { return failure; }

        if (WorldConnectionQuery.Find(context.Project!, command.Player) is null)
        {
            return CommandResult.LocalizedFail("team.target_offline", "TeamTargetOffline_Message", "目标玩家当前不在线。");
        }

        var team = teams.FindForPlayer(actor.Address);
        if (team is null)
        {
            return CommandResult.LocalizedFail("team.invalid_membership", "TeamRequired_Message", "你当前不在有效的队伍中。");
        }

        if (!teams.TryInvite(actor, team.Id, command.Player, out var operation, out var message))
        {
            return Result(false, "team.invitation_failed", message);
        }

        SendPrompt(context, operation!);
        return Pending(
            "team.invitation_pending",
            message with
            {
                Arguments =
                [
                    PlayerDisplayNameResolver.GetDisplayName(context.Project!, command.Player)
                ]
            }
        );
    }

    public static CommandResult Respond(CommandContext context, RespondTeamRequestCommand command)
    {
        if (!TryGetActor(context, out var actor, out var teams, out var failure)) { return failure; }

        var pending = teams.FindOperation(actor, command.OperationId);
        if (command.Accepted &&
            pending is not null &&
            WorldConnectionQuery.Find(context.Project!, pending.JoiningPlayer) is null)
        {
            teams.CancelOperationsFor(pending.JoiningPlayer);
            return CommandResult.LocalizedFail(
                "team.joining_offline",
                "TeamJoiningPlayerOffline_Message",
                "申请加入的玩家已经离线。"
            );
        }

        var changed = teams.TryRespond(
            actor,
            command.OperationId,
            command.Accepted,
            out var operation,
            out var message
        );
        if (operation is not null)
        {
            if (WorldConnectionQuery.Find(context.Project!, operation.Initiator) is { } initiator &&
                initiator.PlayerAddress != actor.Address)
            {
                var result = Result(
                    changed,
                    command.Accepted ? "team.request_accepted" : "team.request_rejected",
                    message
                );
                if (ReferenceEquals(
                        initiator,
                        WorldNetworkBinding.For(context.Project!).RequireNode().Self
                    ))
                {
                    CommandResultPublisher.DisplayLocal(context.Project!, result);
                }
                else
                {
                    NetworkSender.Send(
                        context.Project!,
                        CommandPackage.CreateResult(CommandResponseBuilder.Capture(result), operation.Id.ToString("N")),
                        PackageAudience.To(initiator)
                    );
                }
            }
        }

        if (changed && command.Accepted) { Broadcast(context); }

        return Result(
            changed,
            changed ? command.Accepted ? "team.request_accepted" : "team.request_rejected" : "team.response_failed",
            message
        );
    }

    private static SubsystemPlayers Players(CommandContext context) =>
        context.Project!.FindSubsystem<SubsystemPlayers>(true)!;

    private static bool TryGetActor(
        CommandContext context,
        out WorldPlayer actor,
        out TeamStore teams,
        out CommandResult failure
    )
    {
        actor = context.Principal.Subject!;
        teams = null!;
        if (context.Project is null ||
            actor is null ||
            !ReferenceEquals(Players(context).Store.Find(actor.Address), actor) ||
            actor.Ban is not null ||
            WorldConnectionQuery.Find(context.Project!, actor.Address) is null ||
            !WorldRuntimeContext.For(context.Project).IsAuthority)
        {
            failure = CommandResult.LocalizedFail(
                "team.player_required",
                "TeamOnlinePlayerRequired_Message",
                "组队操作需要已准入的在线玩家。"
            );
            return false;
        }

        teams = context.Project.FindSubsystem<SubsystemTeams>(true)!.Store;
        failure = null!;
        return true;
    }

    private static bool TryStart(TeamStore teams, WorldPlayer actor, out CommandResult failure)
    {
        if (teams.TryBeginOperation(actor, out var error))
        {
            failure = null!;
            return true;
        }

        failure = Result(false, "team.rate_limited", error!);
        return false;
    }

    private static void SendPrompt(CommandContext context, TeamOperation operation)
    {
        var responder = WorldConnectionQuery.Find(context.Project!, operation.Responder)!;
        var package = TeamPackageBuilder.CreatePrompt(operation);
        NetworkSender.Send(context.Project!, package, PackageAudience.To(responder));
    }

    private static void Broadcast(CommandContext context)
    {
        NetworkSender.Send(
            context.Project!,
            TeamPackageBuilder.CreateSnapshot(context.Project!.FindSubsystem<SubsystemTeams>(true)!),
            PackageAudience.Global
        );
    }

    private static CommandResult Result(bool success, string code, TeamMessage message)
    {
        return success
            ? CommandResult.LocalizedOk(code, message.Key, message.Fallback, message.Arguments.ToArray())
            : CommandResult.LocalizedFail(code, message.Key, message.Fallback, message.Arguments.ToArray());
    }

    private static CommandResult Pending(string code, TeamMessage message)
    {
        return CommandResult.LocalizedPending(code, message.Key, message.Fallback, message.Arguments.ToArray());
    }
}
