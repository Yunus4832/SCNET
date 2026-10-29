using Game.Network;
using Game.Players;
using Game.Protocol.Identity;

namespace Game.Commands;

internal static class PlayerModerationCommandHandlers
{
    public static void Register(IModCommands commands, ModId owner)
    {
        var permission = new ResourceId(owner, "server.players.ban");
        var description = Text("PlayerBanManage_Description", "管理存档主体封禁");
        commands.Permissions.Register(
            permission,
            new CommandPermissionDefinition(CommandDomain.Server, description: description)
        );
        commands.Register(
            new ResourceId(owner, "player/ban"),
            new CommandDefinition<BanPlayerCommand>(
                Ban,
                CommandDomain.Server,
                Text("PlayerBan_Description", "封禁主体并断开其会话"),
                permission,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/unban"),
            new CommandDefinition<UnbanPlayerCommand>(
                Unban,
                CommandDomain.Server,
                Text("PlayerUnban_Description", "解除主体封禁并保留角色资产"),
                permission,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/bans"),
            new CommandDefinition<ListPlayerBansCommand>(
                ListBans,
                CommandDomain.Server,
                Text("PlayerBans_Description", "列出存档主体封禁"),
                permission,
                supportsRemoteInvocation: true
            )
        );
        var mutePermission = new ResourceId(owner, "server.players.mute");
        commands.Permissions.Register(
            mutePermission,
            new CommandPermissionDefinition(
                CommandDomain.Server,
                description: Text("PlayerMuteManage_Description", "管理存档主体禁言")
            )
        );
        commands.Register(
            new ResourceId(owner, "player/mute"),
            new CommandDefinition<SetPlayerMuteCommand>(
                SetMute,
                CommandDomain.Server,
                Text("PlayerMute_Description", "设置主体禁言状态"),
                mutePermission,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/muted"),
            new CommandDefinition<ListMutedPlayersCommand>(
                ListMuted,
                CommandDomain.Server,
                Text("PlayerMutedList_Description", "列出被禁言的主体"),
                mutePermission,
                supportsRemoteInvocation: true
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "text/player"),
            new TextCommand(
                "player",
                Text("PlayerModeration_Description", "管理存档主体的封禁与禁言"),
                [
                    new CommandRoute(
                        [
                            new CommandLiteral("ban"),
                            new CommandArgument("player", SuggestionProvider: SuggestPlayers),
                            new CommandArgument("reason")
                        ],
                        typeof(BanPlayerCommand),
                        arguments => new BanPlayerCommand(
                            arguments.Get<string>("player"),
                            arguments.Get<string>("reason")
                        ),
                        Text("PlayerBan_Description", "封禁主体并断开其会话")
                    ),
                    new CommandRoute(
                        [
                            new CommandLiteral("unban"),
                            new CommandArgument("player", SuggestionProvider: SuggestBannedPlayers)
                        ],
                        typeof(UnbanPlayerCommand),
                        arguments => new UnbanPlayerCommand(arguments.Get<string>("player")),
                        Text("PlayerUnban_Description", "解除主体封禁并保留角色资产")
                    ),
                    new CommandRoute(
                        [new CommandLiteral("mute"), new CommandArgument("player", SuggestionProvider: SuggestPlayers)],
                        typeof(SetPlayerMuteCommand),
                        arguments => new SetPlayerMuteCommand(arguments.Get<string>("player"), true),
                        Text("PlayerMute_Description", "设置主体禁言状态")
                    ),
                    new CommandRoute(
                        [
                            new CommandLiteral("unmute"),
                            new CommandArgument("player", SuggestionProvider: SuggestMutedPlayers)
                        ],
                        typeof(SetPlayerMuteCommand),
                        arguments => new SetPlayerMuteCommand(arguments.Get<string>("player"), false),
                        Text("PlayerUnmute_Description", "解除主体禁言")
                    ),
                    new CommandRoute(
                        [new CommandLiteral("muted")],
                        typeof(ListMutedPlayersCommand),
                        _ => new ListMutedPlayersCommand(),
                        Text("PlayerMutedList_Description", "列出被禁言的主体")
                    ),
                    new CommandRoute(
                        [new CommandLiteral("bans")],
                        typeof(ListPlayerBansCommand),
                        _ => new ListPlayerBansCommand(),
                        Text("PlayerBans_Description", "列出存档主体封禁")
                    )
                ]
            )
        );
    }

    private static CommandResult Ban(CommandContext context, BanPlayerCommand command)
    {
        if (!TryGetPlayer(context, command.Player, out var player, out var failure)) { return failure!; }

        var reason = command.Reason.Trim();
        if (reason.Length is < 1 or > 512 || reason.Any(char.IsControl))
        {
            return CommandResult.LocalizedFail(
                "player.ban.invalid_reason",
                "PlayerBanReasonInvalid_Message",
                "封禁原因必须为 1 到 512 个有效字符。"
            );
        }

        context.Project!.FindSubsystem<SubsystemPlayers>(true)!.Store.Ban(
            player!.Address,
            reason,
            context.Principal.Subject?.Address,
            DateTimeOffset.UtcNow
        );
        foreach (var session in CommonLib.Net.Clients.Values.Where(session => session.PlayerAddress == player.Address)
                     .ToArray())
        {
            // Admission checks the persisted ban immediately; defer connection teardown until command dispatch completes.
            CommonLib.Net.RemoveClient(session, reason);
        }

        return CommandResult.LocalizedOk(
            "player.banned",
            "PlayerBanned_Message",
            "已封禁主体 {0}：{1}",
            player.Address.Value,
            reason
        );
    }

    private static CommandResult Unban(CommandContext context, UnbanPlayerCommand command)
    {
        if (!TryGetPlayer(context, command.Player, out var player, out var failure)) { return failure!; }

        if (!context.Project!.FindSubsystem<SubsystemPlayers>(true)!.Store.Unban(player!.Address))
        {
            return CommandResult.LocalizedFail(
                "player.not_banned",
                "PlayerNotBanned_Message",
                "主体 {0} 未被封禁。",
                player.Address.Value
            );
        }

        return CommandResult.LocalizedOk(
            "player.unbanned",
            "PlayerUnbanned_Message",
            "已解除主体 {0} 的封禁。",
            player.Address.Value
        );
    }

    private static CommandResult ListBans(CommandContext context, ListPlayerBansCommand command)
    {
        if (context.Project is null) { return NoWorld(); }

        var players = context.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.Entries
            .Where(player => player.Ban is not null)
            .OrderBy(player => player.Address.Value, StringComparer.Ordinal).ToArray();
        return CommandResult.LocalizedHistoryOk(
            "player.bans",
            "PlayerBans_Message",
            "存档主体封禁：\n{0}",
            players.Length == 0
                ? "-"
                : string.Join(
                    "\n",
                    players.Select(player => $"{player.Address}: {player.Ban!.Reason} ({player.Ban.CreatedAt:O})")
                )
        );
    }

    private static CommandResult SetMute(CommandContext context, SetPlayerMuteCommand command)
    {
        if (!TryGetPlayer(context, command.Player, out var player, out var failure)) { return failure!; }

        player!.IsMuted = command.IsMuted;
        return command.IsMuted
            ? CommandResult.LocalizedOk("player.muted", "PlayerMuted_Message", "已禁言主体 {0}。", player.Address.Value)
            : CommandResult.LocalizedOk(
                "player.unmuted",
                "PlayerUnmuted_Message",
                "已解除主体 {0} 的禁言。",
                player.Address.Value
            );
    }

    private static CommandResult ListMuted(CommandContext context, ListMutedPlayersCommand command)
    {
        if (context.Project is null) { return NoWorld(); }

        var addresses = context.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.Entries
            .Where(player => player.IsMuted)
            .OrderBy(player => player.Address.Value, StringComparer.Ordinal).Select(player => player.Address.Value)
            .ToArray();
        return CommandResult.LocalizedHistoryOk(
            "player.muted_list",
            "PlayerMutedList_Message",
            "存档主体禁言：\n{0}",
            addresses.Length == 0 ? "-" : string.Join("\n", addresses)
        );
    }

    private static bool TryGetPlayer(
        CommandContext context,
        string value,
        out WorldPlayer? player,
        out CommandResult? failure
    )
    {
        player = null;
        if (context.Project is null)
        {
            failure = NoWorld();
            return false;
        }

        try
        {
            player = context.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.Find(new PlayerAddress(value));
        }
        catch (ArgumentException) { }

        failure = player is null
            ? CommandResult.LocalizedFail("player.not_found", "PlayerIdentityNotFound_Message", "找不到主体地址：{0}。", value)
            : null;
        return player is not null;
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestPlayers(CommandSuggestionContext context) =>
        Suggest(context, _ => true);

    private static IEnumerable<CommandArgumentSuggestion> SuggestBannedPlayers(CommandSuggestionContext context) =>
        Suggest(context, player => player.Ban is not null);

    private static IEnumerable<CommandArgumentSuggestion> SuggestMutedPlayers(CommandSuggestionContext context) =>
        Suggest(context, player => player.IsMuted);

    private static IEnumerable<CommandArgumentSuggestion> Suggest(
        CommandSuggestionContext context,
        Func<WorldPlayer, bool> predicate
    )
    {
        return (context.Project?.FindSubsystem<SubsystemPlayers>(true)!.Store.Entries ?? [])
            .Where(predicate)
            .Select(player => new CommandArgumentSuggestion(
                    player.Address.Value,
                    LocalizedText.Literal(player.DisplayName)
                )
            );
    }

    private static LocalizedText Text(string key, string fallback) => new("Commands", key, fallback);

    private static CommandResult NoWorld() =>
        CommandResult.LocalizedFail("player.project_required", "NoWorld_Message", "当前没有已加载的世界。");
}
