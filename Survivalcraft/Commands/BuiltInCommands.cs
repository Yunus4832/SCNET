using EntitySystem.Core;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;
using Game.Subsystems;

namespace Game.Commands;

public static class BuiltInCommands
{
    public static GameCommand CreateHelp()
    {
        return new GameCommand(
            "help",
            "显示可用指令",
            [
                new CommandRoute([], ExecuteHelp, "列出当前可用的指令"),
                new CommandRoute(
                    [new CommandArgument("command")],
                    ExecuteHelpForCommand,
                    "显示指定指令的用法")
            ],
            ["?"]);
    }

    public static GameCommand CreateTime()
    {
        var choices = new[] { "sunrise", "day", "sunset", "night" };
        return new GameCommand(
            "time",
            "查询或设置世界时间",
            [
                new CommandRoute(
                    [new CommandLiteral("get")],
                    ExecuteTimeGet,
                    "显示当前世界时间"),
                new CommandRoute(
                    [
                        new CommandLiteral("set"),
                        new CommandArgument("value", CommandArgumentKind.String, choices)
                    ],
                    ExecuteTimeSet,
                    "设置世界时间",
                    "world.time.set")
            ]);
    }

    public static GameCommand CreateStop()
    {
        return new GameCommand(
            "stop",
            "保存世界并停止 Headless 服务端",
            [
                new CommandRoute(
                    [],
                    ExecuteStop,
                    "停止 Headless 服务端",
                    "server.stop",
                    CommandSourcePolicy.ServerConsoleOnly,
                    CommandGrantPolicy.NonGrantable)
            ],
            executionEnvironment: CommandExecutionEnvironment.HeadlessServer);
    }

    public static GameCommand CreateAuth()
    {
        return new GameCommand(
            "auth",
            "认领服务器管理员身份",
            [
                new CommandRoute(
                    [],
                    ExecuteAuthHelp,
                    "显示服务器认领帮助"),
                new CommandRoute(
                    [
                        new CommandLiteral("claim"),
                        new CommandArgument("code")
                    ],
                    ExecuteAuthClaim,
                    "使用认领码初始化在线玩家的管理权限",
                    sourcePolicy: CommandSourcePolicy.PlayerOnly),
                new CommandRoute(
                    [new CommandLiteral("status")],
                    ExecuteAuthStatus,
                    "查看服务器认领状态",
                    sourcePolicy: CommandSourcePolicy.ServerConsoleOnly),
                new CommandRoute(
                    [new CommandLiteral("code")],
                    ExecuteAuthCode,
                    "显示当前服务器认领码",
                    sourcePolicy: CommandSourcePolicy.ServerConsoleOnly),
                new CommandRoute(
                    [new CommandLiteral("regenerate")],
                    ExecuteAuthRegenerate,
                    "重新生成服务器认领码",
                    sourcePolicy: CommandSourcePolicy.ServerConsoleOnly)
            ]);
    }

    public static GameCommand CreatePermission()
    {
        return new GameCommand(
            "permission",
            "查看和管理玩家指令权限",
            [
                new CommandRoute(
                    [],
                    ExecutePermissionHelp,
                    "显示授权指令帮助"),
                new CommandRoute(
                    [new CommandLiteral("players")],
                    ExecutePermissionPlayers,
                    "列出当前可授权玩家",
                    CommandPermissionSet.GrantPermission),
                new CommandRoute(
                    [new CommandLiteral("nodes")],
                    ExecutePermissionNodes,
                    "列出当前可授权权限节点",
                    CommandPermissionSet.GrantPermission),
                new CommandRoute(
                    [new CommandLiteral("list")],
                    ExecutePermissionListSelf,
                    "查看自己的指令权限"),
                new CommandRoute(
                    [
                        new CommandLiteral("list"),
                        PlayerArgument()
                    ],
                    ExecutePermissionListPlayer,
                    "查看指定玩家的指令权限",
                    CommandPermissionSet.GrantPermission),
                new CommandRoute(
                    [
                        new CommandLiteral("grant"),
                        PlayerArgument(),
                        PermissionArgument(SuggestDelegablePermissionNodes)
                    ],
                    static (context, arguments) =>
                        ExecutePermissionGrant(context, arguments, false),
                    "授予玩家使用权限",
                    CommandPermissionSet.GrantPermission),
                new CommandRoute(
                    [
                        new CommandLiteral("delegate"),
                        PlayerArgument(),
                        PermissionArgument(SuggestDelegablePermissionNodes)
                    ],
                    static (context, arguments) =>
                        ExecutePermissionGrant(context, arguments, true),
                    "授予玩家使用及再授权权限",
                    CommandPermissionSet.GrantPermission),
                new CommandRoute(
                    [
                        new CommandLiteral("revoke"),
                        PlayerArgument(),
                        PermissionArgument(SuggestRevocablePermissionNodes)
                    ],
                    ExecutePermissionRevoke,
                    "撤销玩家的指定权限",
                    CommandPermissionSet.GrantPermission)
            ],
            ["perm"]);
    }

    private static CommandArgument PlayerArgument()
    {
        return new CommandArgument(
            "player",
            SuggestionProvider: SuggestPlayers);
    }

    private static CommandArgument PermissionArgument(
        Func<CommandSuggestionContext, IEnumerable<CommandArgumentSuggestion>> provider)
    {
        return new CommandArgument(
            "permission",
            SuggestionProvider: provider);
    }

    private static CommandResult ExecuteHelp(CommandContext context, CommandArguments arguments)
    {
        var names = context.Registry.Entries
            .Where(entry =>
                CommandRegistry.IsAvailable(entry.Command) &&
                entry.Command.Routes.Any(route =>
                    route.IsSourceAllowed(context.Source) &&
                    context.Principal.HasPermission(route.RequiredPermission)))
            .Select(entry => "/" + entry.Command.Name)
            .ToArray();
        return CommandResult.Ok(
            names.Length == 0 ? "当前没有可用指令。" : $"可用指令：{string.Join("、", names)}",
            "command.help");
    }

    private static CommandResult ExecuteHelpForCommand(CommandContext context, CommandArguments arguments)
    {
        var name = arguments.Get<string>("command").TrimStart('/');
        if (!context.Registry.TryFind(name, out var registered) || registered is null)
        {
            return CommandResult.Fail("command.unknown", $"未知指令：{name}。");
        }

        var routes = registered.Command.Routes
            .Where(route =>
                CommandRegistry.IsAvailable(registered.Command) &&
                route.IsSourceAllowed(context.Source) &&
                context.Principal.HasPermission(route.RequiredPermission))
            .Select(route =>
            {
                var suffix = string.Join(
                    " ",
                    route.Segments.Select(segment => segment switch
                    {
                        CommandLiteral literal => literal.Value,
                        CommandArgument argument => $"<{argument.Name}>",
                        _ => string.Empty
                    }));
                return string.IsNullOrEmpty(suffix)
                    ? $"/{registered.Command.Name}"
                    : $"/{registered.Command.Name} {suffix}";
            })
            .ToArray();
        return routes.Length == 0
            ? CommandResult.Fail("command.forbidden", "你没有查看该指令的权限。")
            : CommandResult.Ok(
                $"{registered.Command.Description}。用法：{string.Join("；", routes)}",
                "command.help.detail");
    }

    private static CommandResult ExecuteTimeGet(CommandContext context, CommandArguments arguments)
    {
        if (context.Project is null)
        {
            return CommandResult.Fail("command.no_world", "当前没有加载世界。");
        }

        var time = context.Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        return CommandResult.Ok(
            $"当前时间：{time.CalculateTimeOfDay():0.000}（第 {time.Day:0.00} 天）",
            "world.time");
    }

    private static CommandResult ExecutePermissionListSelf(
        CommandContext context,
        CommandArguments arguments)
    {
        if (context.Principal.Player is null)
        {
            return CommandResult.Fail(
                "permission.player_required",
                "服务器控制台请指定玩家：permission list <player>。");
        }

        return FormatPermissionList(context.Principal.Player);
    }

    private static CommandResult ExecuteAuthHelp(
        CommandContext context,
        CommandArguments arguments)
    {
        if (context.Project is null)
        {
            return CommandResult.Fail("command.no_world", "当前没有加载世界。");
        }

        return ServerAdministrationBootstrap.IsClaimed(context.Project)
            ? CommandResult.Ok("服务器管理员已经完成首次认领。", "auth.claimed")
            : CommandResult.Ok(
                context.Source is CommandSource.ServerConsole
                    ? "服务器尚未认领。使用 auth code 查看认领码，玩家上线后执行 /auth claim <认领码>。"
                    : "服务器尚未认领。请输入 /auth claim <认领码> 完成首次管理员授权。",
                "auth.unclaimed");
    }

    private static CommandResult ExecuteAuthClaim(
        CommandContext context,
        CommandArguments arguments)
    {
        if (!EnsureServer(out var failure))
        {
            return failure;
        }

        if (context.Project is null)
        {
            return CommandResult.Fail("command.no_world", "当前没有加载世界。");
        }

        if (context.Principal.Player is not { } player)
        {
            return CommandResult.Fail(
                "auth.player_required",
                "认领必须由已连接服务器的在线玩家执行。");
        }

        var result = ServerAdministrationBootstrap.TryClaim(
            context.Project,
            player,
            arguments.Get<string>("code"));
        if (!result.Success)
        {
            return CommandResult.Fail(result.Code, result.Message);
        }

        SynchronizePermissions(player);
        GameManager.SaveProject(
            waitForCompletion: false,
            showErrorDialog: RunMode.Value is RunModeType.Gui);
        return CommandResult.Ok(result.Message, result.Code);
    }

    private static CommandResult ExecuteAuthStatus(
        CommandContext context,
        CommandArguments arguments)
    {
        if (context.Project is null)
        {
            return CommandResult.Fail("command.no_world", "当前没有加载世界。");
        }

        return ServerAdministrationBootstrap.IsClaimed(context.Project)
            ? CommandResult.Ok("服务器管理员已经完成首次认领。", "auth.claimed")
            : CommandResult.Ok(
                "服务器尚未认领，必须由在线玩家提交认领码。",
                "auth.unclaimed");
    }

    private static CommandResult ExecuteAuthCode(
        CommandContext context,
        CommandArguments arguments)
    {
        if (context.Project is null)
        {
            return CommandResult.Fail("command.no_world", "当前没有加载世界。");
        }

        return ServerAdministrationBootstrap.TryGetClaimCode(context.Project, out var code)
            ? CommandResult.SensitiveOk(
                $"认领码：{code}。在线玩家执行 /auth claim {code}",
                "auth.code")
            : CommandResult.Fail("auth.already_claimed", "服务器管理员已经完成首次认领。");
    }

    private static CommandResult ExecuteAuthRegenerate(
        CommandContext context,
        CommandArguments arguments)
    {
        if (context.Project is null)
        {
            return CommandResult.Fail("command.no_world", "当前没有加载世界。");
        }

        return ServerAdministrationBootstrap.TryRegenerateClaimCode(context.Project, out var code)
            ? CommandResult.SensitiveOk(
                $"已重新生成认领码：{code}。在线玩家执行 /auth claim {code}",
                "auth.regenerated")
            : CommandResult.Fail("auth.already_claimed", "服务器管理员已经完成首次认领。");
    }

    private static CommandResult ExecutePermissionHelp(
        CommandContext context,
        CommandArguments arguments)
    {
        var players = GetPlayers(context.Project).Select(player => player.Name).ToArray();
        var nodes = context.Registry.GetPermissionNodes()
            .Where(node => context.Registry.CanGrantPermission(
                node,
                context.Principal,
                context.Source))
            .ToArray();
        var playerText = players.Length == 0 ? "无在线玩家" : string.Join("、", players);
        var nodeText = nodes.Length == 0 ? "无可再授权节点" : string.Join("、", nodes);
        return CommandResult.Ok(
            "授权用法：" +
            "permission grant <玩家> <权限>（仅使用）；" +
            "permission delegate <玩家> <权限>（允许再授权）；" +
            "permission revoke <玩家> <权限>；" +
            "permission list [玩家]。" +
            $" 当前玩家：{playerText}。可授权节点：{nodeText}。",
            "permission.help");
    }

    private static CommandResult ExecutePermissionPlayers(
        CommandContext context,
        CommandArguments arguments)
    {
        var players = GetPlayers(context.Project).ToArray();
        return CommandResult.Ok(
            players.Length == 0
                ? "当前没有可授权的在线玩家。"
                : $"当前玩家：{string.Join("、", players.Select(player => $"{player.Name} ({player.PlayerGUID})"))}",
            "permission.players");
    }

    private static CommandResult ExecutePermissionNodes(
        CommandContext context,
        CommandArguments arguments)
    {
        var nodes = context.Registry.GetPermissionNodes()
            .Where(node => context.Registry.CanGrantPermission(
                node,
                context.Principal,
                context.Source))
            .ToArray();
        return CommandResult.Ok(
            nodes.Length == 0
                ? "当前没有可再授权的权限节点。"
                : $"可授权节点：{string.Join("、", nodes)}",
            "permission.nodes");
    }

    private static CommandResult ExecutePermissionListPlayer(
        CommandContext context,
        CommandArguments arguments)
    {
        return TryFindPlayer(context, arguments.Get<string>("player"), out var player, out var failure)
            ? FormatPermissionList(player!)
            : failure!;
    }

    private static CommandResult ExecutePermissionGrant(
        CommandContext context,
        CommandArguments arguments,
        bool canDelegate)
    {
        if (!EnsureServer(out var failure))
        {
            return failure;
        }

        string permission;
        try
        {
            permission = CommandPermissionSet.Normalize(arguments.Get<string>("permission"));
        }
        catch (ArgumentException)
        {
            return CommandResult.Fail("permission.invalid", "权限节点格式无效。");
        }

        if (!context.Registry.CanGrantPermission(
                permission,
                context.Principal,
                context.Source))
        {
            return CommandResult.Fail(
                "permission.cannot_delegate",
                $"权限节点 {permission} 不可授权，或你没有对应的管理范围。");
        }

        if (!TryFindPlayer(context, arguments.Get<string>("player"), out var player, out failure))
        {
            return failure!;
        }

        var changed = player!.CommandPermissions.Grant(permission, canDelegate);
        if (changed)
        {
            SynchronizePermissions(player);
        }

        var scope = canDelegate ? "允许再授权" : "仅允许使用";
        return CommandResult.Ok(
            changed
                ? $"已授予 {player.Name} 权限 {permission}（{scope}）。"
                : $"{player.Name} 已拥有相同或更高范围的 {permission} 权限。",
            changed ? "permission.granted" : "permission.unchanged");
    }

    private static CommandResult ExecutePermissionRevoke(
        CommandContext context,
        CommandArguments arguments)
    {
        if (!EnsureServer(out var failure))
        {
            return failure;
        }

        string permission;
        try
        {
            permission = CommandPermissionSet.Normalize(arguments.Get<string>("permission"));
        }
        catch (ArgumentException)
        {
            return CommandResult.Fail("permission.invalid", "权限节点格式无效。");
        }

        if (!context.Registry.CanGrantPermission(
                permission,
                context.Principal,
                context.Source))
        {
            return CommandResult.Fail(
                "permission.cannot_delegate",
                $"权限节点 {permission} 不可撤销，或你没有对应的管理范围。");
        }

        if (!TryFindPlayer(context, arguments.Get<string>("player"), out var player, out failure))
        {
            return failure!;
        }

        if (!player!.CommandPermissions.Revoke(permission))
        {
            return CommandResult.Fail(
                "permission.not_found",
                $"{player.Name} 没有直接持有权限 {permission}。");
        }

        SynchronizePermissions(player);
        return CommandResult.Ok(
            $"已撤销 {player.Name} 的权限 {permission}。",
            "permission.revoked");
    }

    private static CommandResult ExecuteTimeSet(CommandContext context, CommandArguments arguments)
    {
        if (context.Project is null)
        {
            return CommandResult.Fail("command.no_world", "当前没有加载世界。");
        }

        var time = context.Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        var value = arguments.Get<string>("value");
        var target = value.ToLowerInvariant() switch
        {
            "sunrise" => time.SunriseOffset,
            "day" => time.DayOffset,
            "sunset" => time.SunsetOffset,
            "night" => time.NightOffset,
            _ => throw new InvalidOperationException($"Unsupported time value {value}.")
        };
        time.TimeOfDayOffset += target - time.CalculateTimeOfDay();
        if (CommonLib.WorkType == WorkType.Server)
        {
            CommonLib.Net.QueuePackage(
                new SubsystemTimePackage(
                    time.SubsystemGameInfo.TotalElapsedGameTime,
                    time.TimeOfDayOffset
                )
            );
        }

        return CommandResult.Ok($"已将世界时间设置为 {value}。", "world.time.changed");
    }

    private static CommandResult ExecuteStop(CommandContext context, CommandArguments arguments)
    {
        if (context.Source is not CommandSource.ServerConsole ||
            RunMode.Value is not RunModeType.HeadlessServer ||
            CommonLib.WorkType is not WorkType.Server)
        {
            return CommandResult.Fail("server.not_headless", "当前进程不是 Headless 服务端。");
        }

        HeadlessEntry.RequestStop();
        return CommandResult.Ok("服务端正在保存并停止。", "server.stopping");
    }

    private static bool EnsureServer(out CommandResult failure)
    {
        if (CommonLib.WorkType is WorkType.Server)
        {
            failure = null!;
            return true;
        }

        failure = CommandResult.Fail(
            "permission.server_only",
            "授权操作只能在服务器上执行。");
        return false;
    }

    private static bool TryFindPlayer(
        CommandContext context,
        string value,
        out PlayerData? player,
        out CommandResult? failure)
    {
        if (context.Project is null)
        {
            player = null;
            failure = CommandResult.Fail("command.no_world", "当前没有加载世界。");
            return false;
        }

        player = FindPlayer(context.Project, value);
        if (player is not null)
        {
            failure = null;
            return true;
        }

        failure = CommandResult.Fail("permission.player_not_found", $"找不到玩家：{value}。");
        return false;
    }

    private static PlayerData? FindPlayer(Project? project, string value)
    {
        if (project is null)
        {
            return null;
        }

        var players = project.FindSubsystem<SubsystemPlayers>(true)!;
        var isGuid = Guid.TryParse(value, out var guid);
        return players.FindPlayerData(candidate =>
            isGuid
                ? candidate.PlayerGUID == guid
                : string.Equals(candidate.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<PlayerData> GetPlayers(Project? project)
    {
        return project?.FindSubsystem<SubsystemPlayers>(true)?.PlayersData ?? [];
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestPlayers(
        CommandSuggestionContext context)
    {
        return GetPlayers(context.Project).Select(player =>
            new CommandArgumentSuggestion(player.Name, player.PlayerGUID.ToString()));
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestDelegablePermissionNodes(
        CommandSuggestionContext context)
    {
        return context.Registry.GetPermissionNodes()
            .Where(node => context.Registry.CanGrantPermission(
                node,
                context.Principal,
                CommandSource.Player))
            .Select(node => new CommandArgumentSuggestion(node, "可授权权限节点"));
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestRevocablePermissionNodes(
        CommandSuggestionContext context)
    {
        if (context.CompletedTokens.Count < 2 ||
            FindPlayer(context.Project, context.CompletedTokens[1]) is not { } player)
        {
            return [];
        }

        return player.CommandPermissions.Grants
            .Where(grant => context.Registry.CanGrantPermission(
                grant.Permission,
                context.Principal,
                CommandSource.Player))
            .Select(grant => new CommandArgumentSuggestion(
                grant.Permission,
                grant.CanDelegate ? "当前为可再授权" : "当前为仅使用"));
    }

    private static CommandResult FormatPermissionList(PlayerData player)
    {
        var grants = player.CommandPermissions.Grants;
        if (grants.Count == 0)
        {
            return CommandResult.Ok(
                $"{player.Name} 当前没有任何指令权限。",
                "permission.list");
        }

        var values = grants.Select(grant =>
            $"{grant.Permission}（{(grant.CanDelegate ? "可再授权" : "仅使用")}）");
        return CommandResult.Ok(
            $"{player.Name} 的指令权限：{string.Join("、", values)}",
            "permission.list");
    }

    internal static void SynchronizePermissions(PlayerData player)
    {
        if (player.Client is null)
        {
            return;
        }

        var package = CommandPackage.CreatePermissionSnapshot(
            player.PlayerGUID,
            player.CommandPermissions.Grants);
        CommonLib.Net.QueuePackage(package);
    }
}
