using Game.Network;
using Game.Players;
using Game.Protocol.Identity;

namespace Game.Commands;

internal static class PlayerModerationCommandHandlers
{
    public static void Register(IModCommands commands, ModId owner)
    {
        var permission = new ResourceId(owner, "server.users.ban");
        var description = Text("PlayerBanManage_Description", "管理存档主体封禁");
        commands.Permissions.Register(
            permission,
            new CommandPermissionDefinition(CommandDomain.Server, description: description)
        );
        commands.Register(
            new ResourceId(owner, "user/ban"),
            new CommandDefinition<BanUserCommand>(
                Ban,
                CommandDomain.Server,
                Text("PlayerBan_Description", "封禁主体并断开其会话"),
                permission,
                write: static (writer, command) =>
                {
                    writer.Write(command.User);
                    writer.Write(command.Reason);
                },
                read: static reader => new BanUserCommand(reader.ReadString(), reader.ReadString())
            )
        );
        commands.Register(
            new ResourceId(owner, "user/unban"),
            new CommandDefinition<UnbanUserCommand>(
                Unban,
                CommandDomain.Server,
                Text("UserUnban_Description", "解除主体封禁并保留角色资产"),
                permission,
                write: static (writer, command) => writer.Write(command.User),
                read: static reader => new UnbanUserCommand(reader.ReadString())
            )
        );
        commands.Register(
            new ResourceId(owner, "user/bans"),
            new CommandDefinition<ListPlayerBansCommand>(
                ListBans,
                CommandDomain.Server,
                Text("PlayerBans_Description", "列出存档主体封禁"),
                permission,
                write: static (_, _) => { },
                read: static _ => new ListPlayerBansCommand()
            )
        );
        var mutePermission = new ResourceId(owner, "server.users.mute");
        commands.Permissions.Register(
            mutePermission,
            new CommandPermissionDefinition(
                CommandDomain.Server,
                description: Text("UserMuteManage_Description", "管理存档主体禁言")
            )
        );
        commands.Register(
            new ResourceId(owner, "user/mute"),
            new CommandDefinition<SetUserMuteCommand>(
                SetMute,
                CommandDomain.Server,
                Text("UserMute_Description", "设置主体禁言状态"),
                mutePermission,
                write: static (writer, command) =>
                {
                    writer.Write(command.User);
                    writer.Write(command.IsMuted);
                },
                read: static reader => new SetUserMuteCommand(reader.ReadString(), reader.ReadBoolean())
            )
        );
        commands.Register(
            new ResourceId(owner, "user/muted"),
            new CommandDefinition<ListMutedUsersCommand>(
                ListMuted,
                CommandDomain.Server,
                Text("UserMutedList_Description", "列出被禁言的主体"),
                mutePermission,
                write: static (_, _) => { },
                read: static _ => new ListMutedUsersCommand()
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "text/user"),
            new TextCommand(
                "user",
                Text("UserModeration_Description", "管理存档主体的封禁与禁言"),
                [
                    new CommandRoute(
                        [
                            new CommandLiteral("ban"), new CommandArgument("user", SuggestionProvider: SuggestUsers),
                            new CommandArgument("reason")
                        ],
                        typeof(BanUserCommand),
                        arguments => new BanUserCommand(arguments.Get<string>("user"), arguments.Get<string>("reason")),
                        Text("PlayerBan_Description", "封禁主体并断开其会话")
                    ),
                    new CommandRoute(
                        [
                            new CommandLiteral("unban"),
                            new CommandArgument("user", SuggestionProvider: SuggestBannedUsers)
                        ],
                        typeof(UnbanUserCommand),
                        arguments => new UnbanUserCommand(arguments.Get<string>("user")),
                        Text("UserUnban_Description", "解除主体封禁并保留角色资产")
                    ),
                    new CommandRoute(
                        [new CommandLiteral("mute"), new CommandArgument("user", SuggestionProvider: SuggestUsers)],
                        typeof(SetUserMuteCommand),
                        arguments => new SetUserMuteCommand(arguments.Get<string>("user"), true),
                        Text("UserMute_Description", "设置主体禁言状态")
                    ),
                    new CommandRoute(
                        [
                            new CommandLiteral("unmute"),
                            new CommandArgument("user", SuggestionProvider: SuggestMutedUsers)
                        ],
                        typeof(SetUserMuteCommand),
                        arguments => new SetUserMuteCommand(arguments.Get<string>("user"), false),
                        Text("UserUnmute_Description", "解除主体禁言")
                    ),
                    new CommandRoute(
                        [new CommandLiteral("muted")],
                        typeof(ListMutedUsersCommand),
                        _ => new ListMutedUsersCommand(),
                        Text("UserMutedList_Description", "列出被禁言的主体")
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

    private static CommandResult Ban(CommandContext context, BanUserCommand command)
    {
        if (!TryGetUser(context, command.User, out var user, out var failure)) { return failure!; }

        var reason = command.Reason.Trim();
        if (reason.Length is < 1 or > 512 || reason.Any(char.IsControl))
        {
            return CommandResult.LocalizedFail(
                "user.ban.invalid_reason",
                "PlayerBanReasonInvalid_Message",
                "封禁原因必须为 1 到 512 个有效字符。"
            );
        }

        context.Project!.FindSubsystem<SubsystemPlayers>(true)!.Store.Ban(
            user!.Address,
            reason,
            context.Principal.Subject?.Address,
            DateTimeOffset.UtcNow
        );
        foreach (var session in CommonLib.Net.Clients.Values.Where(session => session.PlayerAddress == user.Address)
                     .ToArray())
        {
            // Admission checks the persisted ban immediately; defer connection teardown until command dispatch completes.
            CommonLib.Net.RemoveClient(session, reason);
        }

        return CommandResult.LocalizedOk(
            "user.banned",
            "PlayerBanned_Message",
            "已封禁主体 {0}：{1}",
            user.Address.Value,
            reason
        );
    }

    private static CommandResult Unban(CommandContext context, UnbanUserCommand command)
    {
        if (!TryGetUser(context, command.User, out var user, out var failure)) { return failure!; }

        if (!context.Project!.FindSubsystem<SubsystemPlayers>(true)!.Store.Unban(user!.Address))
        {
            return CommandResult.LocalizedFail(
                "user.not_banned",
                "UserNotBanned_Message",
                "主体 {0} 未被封禁。",
                user.Address.Value
            );
        }

        return CommandResult.LocalizedOk("user.unbanned", "UserUnbanned_Message", "已解除主体 {0} 的封禁。", user.Address.Value);
    }

    private static CommandResult ListBans(CommandContext context, ListPlayerBansCommand command)
    {
        if (context.Project is null) { return NoWorld(); }

        var users = context.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.Entries
            .Where(user => user.Ban is not null)
            .OrderBy(user => user.Address.Value, StringComparer.Ordinal).ToArray();
        return CommandResult.LocalizedHistoryOk(
            "user.bans",
            "PlayerBans_Message",
            "存档主体封禁：\n{0}",
            users.Length == 0
                ? "-"
                : string.Join(
                    "\n",
                    users.Select(user => $"{user.Address}: {user.Ban!.Reason} ({user.Ban.CreatedAt:O})")
                )
        );
    }

    private static CommandResult SetMute(CommandContext context, SetUserMuteCommand command)
    {
        if (!TryGetUser(context, command.User, out var user, out var failure)) { return failure!; }

        user!.IsMuted = command.IsMuted;
        return command.IsMuted
            ? CommandResult.LocalizedOk("user.muted", "UserMuted_Message", "已禁言主体 {0}。", user.Address.Value)
            : CommandResult.LocalizedOk("user.unmuted", "UserUnmuted_Message", "已解除主体 {0} 的禁言。", user.Address.Value);
    }

    private static CommandResult ListMuted(CommandContext context, ListMutedUsersCommand command)
    {
        if (context.Project is null) { return NoWorld(); }

        var addresses = context.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.Entries
            .Where(user => user.IsMuted)
            .OrderBy(user => user.Address.Value, StringComparer.Ordinal).Select(user => user.Address.Value).ToArray();
        return CommandResult.LocalizedHistoryOk(
            "user.muted_list",
            "UserMutedList_Message",
            "存档主体禁言：\n{0}",
            addresses.Length == 0 ? "-" : string.Join("\n", addresses)
        );
    }

    private static bool TryGetUser(
        CommandContext context,
        string value,
        out WorldPlayer? user,
        out CommandResult? failure
    )
    {
        user = null;
        if (context.Project is null)
        {
            failure = NoWorld();
            return false;
        }

        try
        {
            user = context.Project.FindSubsystem<SubsystemPlayers>(true)!.Store.Find(new PlayerAddress(value));
        }
        catch (ArgumentException) { }

        failure = user is null
            ? CommandResult.LocalizedFail("user.not_found", "UserNotFound_Message", "找不到主体地址：{0}。", value)
            : null;
        return user is not null;
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestUsers(CommandSuggestionContext context) =>
        Suggest(context, _ => true);

    private static IEnumerable<CommandArgumentSuggestion> SuggestBannedUsers(CommandSuggestionContext context) =>
        Suggest(context, user => user.Ban is not null);

    private static IEnumerable<CommandArgumentSuggestion> SuggestMutedUsers(CommandSuggestionContext context) =>
        Suggest(context, user => user.IsMuted);

    private static IEnumerable<CommandArgumentSuggestion> Suggest(
        CommandSuggestionContext context,
        Func<WorldPlayer, bool> predicate
    )
    {
        return (context.Project?.FindSubsystem<SubsystemPlayers>(true)!.Store.Entries ?? [])
            .Where(predicate)
            .Select(user => new CommandArgumentSuggestion(user.Address.Value, LocalizedText.Literal(user.DisplayName)));
    }

    private static LocalizedText Text(string key, string fallback) => new("Commands", key, fallback);

    private static CommandResult NoWorld() =>
        CommandResult.LocalizedFail("user.project_required", "NoWorld_Message", "当前没有已加载的世界。");
}
