using EntitySystem.Core;

using Game.Localization;
using Game.Messaging;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Commands;

public static class BuiltInCommands
{
    internal static void Register(CommandRegistry registry, ModId owner)
    {
        Register(new DirectRegistration(registry, owner), owner);
    }

    public static void Register(IModCommands commands, ModId owner)
    {
        var permissionGrant = new ResourceId(owner, "permissions.grant");
        var permissionManageStandard =
            new ResourceId(owner, "permissions.manage.standard");
        var worldTimeSet = new ResourceId(owner, "world.time.set");
        var worldPrecipitationSet =
            new ResourceId(owner, "world.weather.precipitation.set");
        var worldFogSet = new ResourceId(owner, "world.weather.fog.set");
        var worldLightningTrigger =
            new ResourceId(owner, "world.weather.lightning.trigger");
        var worldSeasonSet = new ResourceId(owner, "world.season.set");
        var serverStop = new ResourceId(owner, "server.stop");
        var serverAuthManage = new ResourceId(owner, "server.auth.manage");
        var playerProfileManage =
            new ResourceId(owner, "server.player.profile.manage");

        RegisterPermission(
            commands,
            permissionGrant,
            CommandDomain.Server,
            PermissionGrantPolicy.OperatorOnly,
            implicitGrant: (principal, _) =>
                principal.DelegablePermissions.Count > 0 ||
                principal.HasPermission(permissionManageStandard));
        RegisterPermission(
            commands,
            permissionManageStandard,
            CommandDomain.Server,
            managesStandardPermissions: true);
        RegisterCreativePermission(commands, worldTimeSet);
        RegisterCreativePermission(commands, worldPrecipitationSet);
        RegisterCreativePermission(commands, worldFogSet);
        RegisterCreativePermission(commands, worldLightningTrigger);
        RegisterCreativePermission(commands, worldSeasonSet);
        RegisterPermission(
            commands,
            serverStop,
            CommandDomain.Server,
            PermissionGrantPolicy.OperatorOnly);
        RegisterPermission(
            commands,
            serverAuthManage,
            CommandDomain.Server,
            PermissionGrantPolicy.OperatorOnly);
        RegisterPermission(
            commands,
            playerProfileManage,
            CommandDomain.Server,
            PermissionGrantPolicy.OperatorOnly);

        commands.Register(
            new ResourceId(owner, "help"),
            new CommandDefinition<ShowCommandHelpCommand>(
                ExecuteHelp,
                CommandDomain.World,
                CommandDescription("Help_Description", "显示可用指令"),
                write: static (writer, command) =>
                {
                    writer.Write(command.CommandName is not null);
                    if (command.CommandName is not null)
                    {
                        writer.Write(command.CommandName);
                    }
                },
                read: static reader => new ShowCommandHelpCommand(
                    reader.ReadBoolean() ? reader.ReadString() : null)));
        commands.Register(
            new ResourceId(owner, "world/time/get"),
            new CommandDefinition<GetWorldTimeCommand>(
                ExecuteTimeGet,
                CommandDomain.World,
                CommandDescription("TimeGet_Description", "显示当前世界时间"),
                write: static (_, _) => { },
                read: static _ => new GetWorldTimeCommand()));
        commands.Register(
            new ResourceId(owner, "world/time/set"),
            new CommandDefinition<SetWorldTimeCommand>(
                ExecuteTimeSet,
                CommandDomain.World,
                CommandDescription("TimeSet_Description", "设置世界时间"),
                worldTimeSet,
                write: static (writer, command) => writer.Write(command.Preset),
                read: static reader => new SetWorldTimeCommand(reader.ReadString())));
        commands.Register(
            new ResourceId(owner, "world/time/advance"),
            new CommandDefinition<AdvanceWorldTimeCommand>(
                ExecuteTimeAdvance,
                CommandDomain.World,
                CommandDescription("TimeAdvance_Description", "前进到下一个时间节点"),
                worldTimeSet,
                write: static (_, _) => { },
                read: static _ => new AdvanceWorldTimeCommand()));
        commands.Register(
            new ResourceId(owner, "world/weather/precipitation/set"),
            new CommandDefinition<SetPrecipitationCommand>(
                WorldControlCommandHandlers.SetPrecipitation,
                CommandDomain.World,
                CommandDescription("WeatherRain_Description", "开启或停止降水"),
                worldPrecipitationSet,
                write: static (writer, command) => writer.Write(command.Enabled),
                read: static reader => new SetPrecipitationCommand(reader.ReadBoolean())));
        commands.Register(
            new ResourceId(owner, "world/weather/fog/set"),
            new CommandDefinition<SetFogCommand>(
                WorldControlCommandHandlers.SetFog,
                CommandDomain.World,
                CommandDescription("WeatherFog_Description", "开启或关闭雾气"),
                worldFogSet,
                write: static (writer, command) => writer.Write(command.Enabled),
                read: static reader => new SetFogCommand(reader.ReadBoolean())));
        commands.Register(
            new ResourceId(owner, "world/weather/lightning/trigger_player"),
            new CommandDefinition<TriggerPlayerLightningCommand>(
                WorldControlCommandHandlers.TriggerPlayerLightning,
                CommandDomain.World,
                CommandDescription(
                    "LightningPlayer_Description",
                    "在玩家注视位置附近触发闪电"),
                worldLightningTrigger,
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (_, _) => { },
                read: static _ => new TriggerPlayerLightningCommand()));
        commands.Register(
            new ResourceId(owner, "world/weather/lightning/trigger"),
            new CommandDefinition<TriggerLightningCommand>(
                WorldControlCommandHandlers.TriggerLightning,
                CommandDomain.World,
                CommandDescription("LightningTarget_Description", "在目标附近触发闪电"),
                worldLightningTrigger,
                write: static (writer, command) =>
                {
                    writer.Write(command.Position);
                    writer.Write(command.Direction);
                },
                read: static reader => new TriggerLightningCommand(
                    reader.ReadVector3(),
                    reader.ReadVector3())));
        commands.Register(
            new ResourceId(owner, "world/season/set"),
            new CommandDefinition<SetSeasonCommand>(
                WorldControlCommandHandlers.SetSeason,
                CommandDomain.World,
                CommandDescription("SeasonSet_Description", "设置当前季节"),
                worldSeasonSet,
                write: static (writer, command) =>
                {
                    writer.WriteEnum(command.Season);
                    writer.Write(command.Progress);
                },
                read: static reader => new SetSeasonCommand(
                    reader.ReadEnum<Season>(),
                    reader.ReadSingle())));
        commands.Register(
            new ResourceId(owner, "player/list"),
            new CommandDefinition<ListPlayersCommand>(
                ExecutePlayerList,
                CommandDomain.World,
                CommandDescription("Players_Description", "列出已知玩家及其在线状态"),
                write: static (_, _) => { },
                read: static _ => new ListPlayersCommand()));
        commands.Register(
            new ResourceId(owner, "application/run_mode/get"),
            new CommandDefinition<GetRunModeCommand>(
                ExecuteRunModeGet,
                CommandDomain.Application,
                CommandDescription("RunModeGet_Description", "显示当前运行模式")));
        commands.Register(
            new ResourceId(owner, "application/run_mode/set"),
            new CommandDefinition<SetRunModeCommand>(
                ExecuteRunModeSet,
                CommandDomain.Application,
                CommandDescription(
                    "RunModeSet_Description",
                    "切换运行模式并重启")));
        commands.Register(
            new ResourceId(owner, "application/restart"),
            new CommandDefinition<RestartApplicationCommand>(
                ExecuteApplicationRestart,
                CommandDomain.Application,
                CommandDescription("ApplicationRestart_Description", "重新启动应用")));
        commands.Register(
            new ResourceId(owner, "application/instance/switch"),
            new CommandDefinition<SwitchInstanceCommand>(
                ExecuteInstanceSwitch,
                CommandDomain.Application,
                CommandDescription("InstanceSwitch_Description", "切换数据实例并重启")));
        commands.Register(
            new ResourceId(owner, "application/instance/create"),
            new CommandDefinition<CreateInstanceCommand>(
                ExecuteInstanceCreate,
                CommandDomain.Application,
                CommandDescription("InstanceCreate_Description", "创建数据实例")));
        commands.Register(
            new ResourceId(owner, "application/instance/delete"),
            new CommandDefinition<DeleteInstanceCommand>(
                ExecuteInstanceDelete,
                CommandDomain.Application,
                CommandDescription("InstanceDelete_Description", "删除数据实例")));
        commands.Register(
            new ResourceId(owner, "application/instance/clone"),
            new CommandDefinition<CloneInstanceCommand>(
                ExecuteInstanceClone,
                CommandDomain.Application,
                CommandDescription("InstanceClone_Description", "克隆数据实例")));
        commands.Register(
            new ResourceId(owner, "application/exit"),
            new CommandDefinition<ExitApplicationCommand>(
                ExecuteApplicationExit,
                CommandDomain.Application,
                CommandDescription("ApplicationExit_Description", "退出应用")));
        commands.Register(
            new ResourceId(owner, "application/language/get"),
            new CommandDefinition<GetLanguageCommand>(
                ExecuteLanguageGet,
                CommandDomain.Application,
                CommandDescription("LanguageGet_Description", "显示当前语言")));
        commands.Register(
            new ResourceId(owner, "application/language/set"),
            new CommandDefinition<SetLanguageCommand>(
                ExecuteLanguageSet,
                CommandDomain.Application,
                CommandDescription("LanguageSet_Description", "切换本地界面语言")));
        commands.Register(
            new ResourceId(owner, "server/stop"),
            new CommandDefinition<StopServerCommand>(
                ExecuteStop,
                CommandDomain.Server,
                CommandDescription("StopAction_Description", "停止 Headless 服务端"),
                serverStop,
                CommandHostRequirement.HeadlessServer));
        commands.Register(
            new ResourceId(owner, "server/auth/help"),
            new CommandDefinition<ShowServerAuthHelpCommand>(
                ExecuteAuthHelp,
                CommandDomain.Server,
                CommandDescription("AuthHelp_Description", "显示服务器认领帮助")));
        commands.Register(
            new ResourceId(owner, "server/auth/claim"),
            new CommandDefinition<ClaimServerAdministrationCommand>(
                ExecuteAuthClaim,
                CommandDomain.Server,
                CommandDescription(
                    "AuthClaim_Description",
                    "使用认领码初始化在线玩家的管理权限"),
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (writer, command) => writer.Write(command.Code),
                read: static reader => new ClaimServerAdministrationCommand(reader.ReadString())));
        commands.Register(
            new ResourceId(owner, "server/auth/status"),
            new CommandDefinition<GetServerAuthStatusCommand>(
                ExecuteAuthStatus,
                CommandDomain.Server,
                CommandDescription("AuthStatus_Description", "查看服务器认领状态"),
                serverAuthManage));
        commands.Register(
            new ResourceId(owner, "server/auth/code"),
            new CommandDefinition<GetServerAuthCodeCommand>(
                ExecuteAuthCode,
                CommandDomain.Server,
                CommandDescription("AuthCode_Description", "显示当前服务器认领码"),
                serverAuthManage));
        commands.Register(
            new ResourceId(owner, "server/auth/regenerate"),
            new CommandDefinition<RegenerateServerAuthCodeCommand>(
                ExecuteAuthRegenerate,
                CommandDomain.Server,
                CommandDescription("AuthRegenerate_Description", "重新生成服务器认领码"),
                serverAuthManage));
        commands.Register(
            new ResourceId(owner, "permission/help"),
            new CommandDefinition<ShowPermissionHelpCommand>(
                ExecutePermissionHelp,
                CommandDomain.Server,
                CommandDescription("PermissionHelp_Description", "显示授权命令帮助")));
        commands.Register(
            new ResourceId(owner, "permission/players"),
            new CommandDefinition<ListPermissionPlayersCommand>(
                ExecutePermissionPlayers,
                CommandDomain.Server,
                CommandDescription("PermissionPlayers_Description", "列出当前可授权玩家"),
                permissionGrant));
        commands.Register(
            new ResourceId(owner, "permission/nodes"),
            new CommandDefinition<ListPermissionNodesCommand>(
                ExecutePermissionNodes,
                CommandDomain.Server,
                CommandDescription("PermissionNodes_Description", "列出当前可授权权限节点"),
                permissionGrant));
        commands.Register(
            new ResourceId(owner, "permission/list_self"),
            new CommandDefinition<ListOwnPermissionsCommand>(
                ExecutePermissionListSelf,
                CommandDomain.Server,
                CommandDescription("PermissionListSelf_Description", "查看自己的命令权限"),
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (_, _) => { },
                read: static _ => new ListOwnPermissionsCommand()));
        commands.Register(
            new ResourceId(owner, "permission/list_player"),
            new CommandDefinition<ListPlayerPermissionsCommand>(
                ExecutePermissionListPlayer,
                CommandDomain.Server,
                CommandDescription(
                    "PermissionListPlayer_Description",
                    "查看指定玩家的命令权限"),
                permissionGrant,
                write: static (writer, command) => writer.Write(command.Player),
                read: static reader => new ListPlayerPermissionsCommand(reader.ReadString())));
        commands.Register(
            new ResourceId(owner, "permission/grant"),
            new CommandDefinition<GrantPlayerPermissionCommand>(
                ExecutePermissionGrant,
                CommandDomain.Server,
                CommandDescription("PermissionGrant_Description", "授予玩家命令权限"),
                permissionGrant,
                write: static (writer, command) =>
                {
                    writer.Write(command.Player);
                    writer.Write(command.Permission);
                    writer.Write(command.CanDelegate);
                },
                read: static reader => new GrantPlayerPermissionCommand(
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadBoolean())));
        commands.Register(
            new ResourceId(owner, "permission/revoke"),
            new CommandDefinition<RevokePlayerPermissionCommand>(
                ExecutePermissionRevoke,
                CommandDomain.Server,
                CommandDescription("PermissionRevoke_Description", "撤销玩家命令权限"),
                permissionGrant,
                write: static (writer, command) =>
                {
                    writer.Write(command.Player);
                    writer.Write(command.Permission);
                },
                read: static reader => new RevokePlayerPermissionCommand(
                    reader.ReadString(),
                    reader.ReadString())));
        commands.Register(
            new ResourceId(owner, "player/profile/update_self"),
            new CommandDefinition<UpdateOwnPlayerProfileCommand>(
                PlayerAndMessageCommandHandlers.UpdateOwnPlayerProfile,
                CommandDomain.World,
                CommandDescription(
                    "PlayerProfileSelf_Description",
                    "更新自己的玩家资料"),
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (writer, command) =>
                {
                    writer.Write(command.Name);
                    writer.Write(command.SkinName);
                    writer.WriteEnum(command.PlayerClass);
                },
                read: static reader => new UpdateOwnPlayerProfileCommand(
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadEnum<PlayerClass>())));
        commands.Register(
            new ResourceId(owner, "player/profile/update"),
            new CommandDefinition<UpdatePlayerProfileCommand>(
                PlayerAndMessageCommandHandlers.UpdatePlayerProfile,
                CommandDomain.Server,
                CommandDescription(
                    "PlayerProfileServer_Description",
                    "由服务器更新玩家资料"),
                playerProfileManage));
        commands.Register(
            new ResourceId(owner, "chat/send"),
            new CommandDefinition<SendChatMessageCommand>(
                PlayerAndMessageCommandHandlers.SendChatMessage,
                CommandDomain.World,
                CommandDescription("ChatSend_Description", "发送聊天消息"),
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (writer, command) =>
                {
                    writer.WriteEnum(command.Channel);
                    writer.Write(command.Content);
                },
                read: static reader => new SendChatMessageCommand(
                    reader.ReadEnum<GameMessageChannel>(),
                    reader.ReadString())));
        commands.Register(
            new ResourceId(owner, "team/create"),
            new CommandDefinition<CreateTeamCommand>(
                GroupCommandHandlers.CreateTeam,
                CommandDomain.World,
                CommandDescription("TeamCreate_Description", "创建队伍"),
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (writer, command) => writer.Write(command.Name),
                read: static reader => new CreateTeamCommand(reader.ReadString())));
        commands.Register(
            new ResourceId(owner, "team/request_join"),
            new CommandDefinition<RequestJoinTeamCommand>(
                GroupCommandHandlers.RequestJoin,
                CommandDomain.World,
                CommandDescription("TeamJoin_Description", "申请加入队伍"),
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (writer, command) => writer.Write(command.TeamId),
                read: static reader => new RequestJoinTeamCommand(reader.ReadGuid())));
        commands.Register(
            new ResourceId(owner, "team/invite"),
            new CommandDefinition<InvitePlayerToTeamCommand>(
                GroupCommandHandlers.InvitePlayer,
                CommandDomain.World,
                CommandDescription("TeamInvite_Description", "邀请玩家加入队伍"),
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (writer, command) => writer.Write(command.PlayerId),
                read: static reader => new InvitePlayerToTeamCommand(reader.ReadGuid())));
        commands.Register(
            new ResourceId(owner, "team/respond"),
            new CommandDefinition<RespondTeamRequestCommand>(
                GroupCommandHandlers.Respond,
                CommandDomain.World,
                CommandDescription("TeamRespond_Description", "响应队伍请求"),
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (writer, command) =>
                {
                    writer.Write(command.OperationId);
                    writer.Write(command.Accepted);
                },
                read: static reader => new RespondTeamRequestCommand(
                    reader.ReadGuid(),
                    reader.ReadBoolean())));
        commands.Register(
            new ResourceId(owner, "team/leave"),
            new CommandDefinition<LeaveTeamCommand>(
                GroupCommandHandlers.LeaveTeam,
                CommandDomain.World,
                CommandDescription("TeamLeave_Description", "退出当前队伍"),
                allowedPrincipals: CommandPrincipalKind.Player,
                write: static (_, _) => { },
                read: static _ => new LeaveTeamCommand()));

        commands.Adapters.Register(new ResourceId(owner, "text/help"), CreateHelpText());
        commands.Adapters.Register(new ResourceId(owner, "text/time"), CreateTimeText());
        commands.Adapters.Register(new ResourceId(owner, "text/weather"), CreateWeatherText());
        commands.Adapters.Register(new ResourceId(owner, "text/season"), CreateSeasonText());
        commands.Adapters.Register(new ResourceId(owner, "text/players"), CreatePlayersText());
        commands.Adapters.Register(new ResourceId(owner, "text/run_mode"), CreateRunModeText());
        commands.Adapters.Register(new ResourceId(owner, "text/language"), CreateLanguageText());
        commands.Adapters.Register(new ResourceId(owner, "text/stop"), CreateStopText());
        commands.Adapters.Register(new ResourceId(owner, "text/auth"), CreateAuthText());
        commands.Adapters.Register(new ResourceId(owner, "text/permission"), CreatePermissionText());
        commands.Adapters.Register(new ResourceId(owner, "text/team"), CreateTeamText());
    }

    private static void RegisterCreativePermission(
        IModCommands commands,
        ResourceId id)
    {
        RegisterPermission(
            commands,
            id,
            CommandDomain.World,
            implicitGrant: WorldControlCommandHandlers.IsCreativePlayer);
    }

    private static void RegisterPermission(
        IModCommands commands,
        ResourceId id,
        CommandDomain domain,
        PermissionGrantPolicy grantPolicy = PermissionGrantPolicy.Standard,
        bool managesStandardPermissions = false,
        Func<CommandPrincipal, Project?, bool>? implicitGrant = null)
    {
        commands.Permissions.Register(
            id,
            new CommandPermissionDefinition(
                domain,
                grantPolicy,
                managesStandardPermissions: managesStandardPermissions,
                implicitGrant: implicitGrant));
    }

    internal static TextCommand CreateHelpText()
    {
        return new TextCommand(
            "help",
            CommandDescription("Help_Description", "显示可用指令"),
            [
                new CommandRoute(
                    [],
                    typeof(ShowCommandHelpCommand),
                    _ => new ShowCommandHelpCommand(),
                    CommandDescription("HelpList_Description", "列出当前可用的指令")),
                new CommandRoute(
                    [
                        new CommandArgument(
                            "command",
                            SuggestionProvider: SuggestCommands)
                    ],
                    typeof(ShowCommandHelpCommand),
                    arguments => new ShowCommandHelpCommand(arguments.Get<string>("command")),
                    CommandDescription("HelpDetail_Description", "显示指定指令的用法"))
            ],
            ["?"]);
    }

    internal static TextCommand CreateTimeText()
    {
        var choices = new[] { "sunrise", "day", "sunset", "night" };
        return new TextCommand(
            "time",
            CommandDescription("Time_Description", "查询或设置世界时间"),
            [
                new CommandRoute(
                    [new CommandLiteral("get")],
                    typeof(GetWorldTimeCommand),
                    _ => new GetWorldTimeCommand(),
                    CommandDescription("TimeGet_Description", "显示当前世界时间")),
                new CommandRoute(
                    [
                        new CommandLiteral("set"),
                        new CommandArgument(
                            "value",
                            CommandArgumentKind.String,
                            choices,
                            _ => TimeSuggestions())
                    ],
                    typeof(SetWorldTimeCommand),
                    arguments => new SetWorldTimeCommand(arguments.Get<string>("value")),
                    CommandDescription("TimeSet_Description", "设置世界时间"))
            ]);
    }

    internal static TextCommand CreatePlayersText()
    {
        return new TextCommand(
            "players",
            CommandDescription("Players_Description", "列出已知玩家及其在线状态"),
            [
                new CommandRoute(
                    [],
                    typeof(ListPlayersCommand),
                    _ => new ListPlayersCommand(),
                    CommandDescription("PlayersList_Description", "显示玩家列表"))
            ],
            ["list"]);
    }

    internal static TextCommand CreateRunModeText()
    {
        var modes = new[] { "gui", "headless" };
        return new TextCommand(
            "runmode",
            CommandDescription("RunMode_Description", "查询或切换进程运行模式"),
            [
                new CommandRoute(
                    [],
                    typeof(GetRunModeCommand),
                    _ => new GetRunModeCommand(),
                    CommandDescription("RunModeGet_Description", "显示当前运行模式")),
                new CommandRoute(
                    [
                        new CommandArgument(
                            "mode",
                            CommandArgumentKind.String,
                            modes)
                    ],
                    typeof(SetRunModeCommand),
                    arguments => new SetRunModeCommand(
                        ParseRunMode(arguments.Get<string>("mode"))),
                    CommandDescription(
                        "RunModeSet_Description",
                        "切换运行模式并重启到主菜单"))
            ],
            ["mode"]);
    }

    internal static TextCommand CreateLanguageText()
    {
        return new TextCommand(
            "language",
            CommandDescription("Language_Description", "查询或切换本地界面语言"),
            [
                new CommandRoute(
                    [],
                    typeof(GetLanguageCommand),
                    _ => new GetLanguageCommand(),
                    CommandDescription("LanguageGet_Description", "显示当前本地语言")),
                new CommandRoute(
                    [
                        new CommandArgument(
                            "language",
                            SuggestionProvider: SuggestLanguages)
                    ],
                    typeof(SetLanguageCommand),
                    arguments => new SetLanguageCommand(
                        arguments.Get<string>("language")),
                    CommandDescription("LanguageSet_Description", "切换本地界面语言"))
            ],
            ["lang"]);
    }

    internal static TextCommand CreateStopText()
    {
        return new TextCommand(
            "stop",
            CommandDescription("Stop_Description", "保存世界并停止 Headless 服务端"),
            [
                new CommandRoute(
                    [],
                    typeof(StopServerCommand),
                    _ => new StopServerCommand(),
                    CommandDescription("StopAction_Description", "停止 Headless 服务端"))
            ]);
    }

    internal static TextCommand CreateWeatherText()
    {
        var toggleChoices = new[] { "enable", "disable" };
        return new TextCommand(
            "weather",
            CommandDescription("Weather_Description", "控制世界天气"),
            [
                new CommandRoute(
                    [
                        new CommandLiteral("rain"),
                        new CommandArgument(
                            "enabled",
                            CommandArgumentKind.String,
                            toggleChoices,
                            _ => ToggleSuggestions(
                                "WeatherRainEnable_Description",
                                "开启降水",
                                "WeatherRainDisable_Description",
                                "停止降水"))
                    ],
                    typeof(SetPrecipitationCommand),
                    arguments => new SetPrecipitationCommand(
                        ParseToggle(arguments.Get<string>("enabled"))),
                    CommandDescription("WeatherRain_Description", "控制降水")),
                new CommandRoute(
                    [
                        new CommandLiteral("fog"),
                        new CommandArgument(
                            "enabled",
                            CommandArgumentKind.String,
                            toggleChoices,
                            _ => ToggleSuggestions(
                                "WeatherFogEnable_Description",
                                "开启雾气",
                                "WeatherFogDisable_Description",
                                "关闭雾气"))
                    ],
                    typeof(SetFogCommand),
                    arguments => new SetFogCommand(
                        ParseToggle(arguments.Get<string>("enabled"))),
                    CommandDescription("WeatherFog_Description", "控制雾气"))
            ]);
    }

    internal static TextCommand CreateSeasonText()
    {
        var seasons = new[] { "summer", "autumn", "winter", "spring" };
        var progressStages = new[] { "start", "middle", "end" };
        return new TextCommand(
            "season",
            CommandDescription("Season_Description", "设置当前季节"),
            [
                new CommandRoute(
                    [
                        new CommandLiteral("set"),
                        new CommandArgument(
                            "season",
                            CommandArgumentKind.String,
                            seasons,
                            _ => SeasonSuggestions())
                    ],
                    typeof(SetSeasonCommand),
                    arguments => new SetSeasonCommand(
                        Enum.Parse<Season>(
                            arguments.Get<string>("season"),
                            ignoreCase: true),
                        0f),
                    CommandDescription("SeasonSet_Description", "设置季节")),
                new CommandRoute(
                    [
                        new CommandLiteral("set"),
                        new CommandArgument(
                            "season",
                            CommandArgumentKind.String,
                            seasons,
                            _ => SeasonSuggestions()),
                        new CommandArgument(
                            "progress",
                            CommandArgumentKind.String,
                            progressStages,
                            _ => SeasonProgressSuggestions())
                    ],
                    typeof(SetSeasonCommand),
                    arguments => new SetSeasonCommand(
                        Enum.Parse<Season>(
                            arguments.Get<string>("season"),
                            ignoreCase: true),
                        ParseSeasonProgress(arguments.Get<string>("progress"))),
                    CommandDescription("SeasonProgress_Description", "设置季节及进度"))
            ]);
    }

    internal static TextCommand CreateAuthText()
    {
        return new TextCommand(
            "auth",
            CommandDescription("Auth_Description", "认领服务器管理员身份"),
            [
                new CommandRoute(
                    [],
                    typeof(ShowServerAuthHelpCommand),
                    _ => new ShowServerAuthHelpCommand(),
                    CommandDescription("AuthHelp_Description", "显示服务器认领帮助")),
                new CommandRoute(
                    [
                        new CommandLiteral("claim"),
                        new CommandArgument("code")
                    ],
                    typeof(ClaimServerAdministrationCommand),
                    arguments => new ClaimServerAdministrationCommand(
                        arguments.Get<string>("code")),
                    CommandDescription(
                        "AuthClaim_Description",
                        "使用认领码初始化在线玩家的管理权限")),
                new CommandRoute(
                    [new CommandLiteral("status")],
                    typeof(GetServerAuthStatusCommand),
                    _ => new GetServerAuthStatusCommand(),
                    CommandDescription("AuthStatus_Description", "查看服务器认领状态")),
                new CommandRoute(
                    [new CommandLiteral("code")],
                    typeof(GetServerAuthCodeCommand),
                    _ => new GetServerAuthCodeCommand(),
                    CommandDescription("AuthCode_Description", "显示当前服务器认领码")),
                new CommandRoute(
                    [new CommandLiteral("regenerate")],
                    typeof(RegenerateServerAuthCodeCommand),
                    _ => new RegenerateServerAuthCodeCommand(),
                    CommandDescription(
                        "AuthRegenerate_Description",
                        "重新生成服务器认领码"))
            ]);
    }

    internal static TextCommand CreatePermissionText()
    {
        return new TextCommand(
            "permission",
            CommandDescription("Permission_Description", "查看和管理玩家指令权限"),
            [
                new CommandRoute(
                    [],
                    typeof(ShowPermissionHelpCommand),
                    _ => new ShowPermissionHelpCommand(),
                    CommandDescription("PermissionHelp_Description", "显示授权指令帮助")),
                new CommandRoute(
                    [new CommandLiteral("players")],
                    typeof(ListPermissionPlayersCommand),
                    _ => new ListPermissionPlayersCommand(),
                    CommandDescription(
                        "PermissionPlayers_Description",
                        "列出当前可授权玩家")),
                new CommandRoute(
                    [new CommandLiteral("nodes")],
                    typeof(ListPermissionNodesCommand),
                    _ => new ListPermissionNodesCommand(),
                    CommandDescription(
                        "PermissionNodes_Description",
                        "列出当前可授权权限节点")),
                new CommandRoute(
                    [new CommandLiteral("list")],
                    typeof(ListOwnPermissionsCommand),
                    _ => new ListOwnPermissionsCommand(),
                    CommandDescription(
                        "PermissionListSelf_Description",
                        "查看自己的指令权限")),
                new CommandRoute(
                    [
                        new CommandLiteral("list"),
                        PlayerArgument()
                    ],
                    typeof(ListPlayerPermissionsCommand),
                    arguments => new ListPlayerPermissionsCommand(
                        arguments.Get<string>("player")),
                    CommandDescription(
                        "PermissionListPlayer_Description",
                        "查看指定玩家的指令权限")),
                new CommandRoute(
                    [
                        new CommandLiteral("grant"),
                        PlayerArgument(),
                        PermissionArgument(SuggestDelegablePermissionNodes)
                    ],
                    typeof(GrantPlayerPermissionCommand),
                    arguments => new GrantPlayerPermissionCommand(
                        arguments.Get<string>("player"),
                        arguments.Get<string>("permission"),
                        false),
                    CommandDescription(
                        "PermissionGrant_Description",
                        "授予玩家使用权限")),
                new CommandRoute(
                    [
                        new CommandLiteral("delegate"),
                        PlayerArgument(),
                        PermissionArgument(SuggestDelegablePermissionNodes)
                    ],
                    typeof(GrantPlayerPermissionCommand),
                    arguments => new GrantPlayerPermissionCommand(
                        arguments.Get<string>("player"),
                        arguments.Get<string>("permission"),
                        true),
                    CommandDescription(
                        "PermissionDelegate_Description",
                        "授予玩家使用及再授权权限")),
                new CommandRoute(
                    [
                        new CommandLiteral("revoke"),
                        PlayerArgument(),
                        PermissionArgument(SuggestRevocablePermissionNodes)
                    ],
                    typeof(RevokePlayerPermissionCommand),
                    arguments => new RevokePlayerPermissionCommand(
                        arguments.Get<string>("player"),
                        arguments.Get<string>("permission")),
                    CommandDescription(
                        "PermissionRevoke_Description",
                        "撤销玩家的指定权限"))
            ],
            ["perm"]);
    }

    internal static TextCommand CreateTeamText()
    {
        return new TextCommand(
            "team",
            CommandDescription("Team_Description", "创建、加入或退出队伍"),
            [
                new CommandRoute(
                    [
                        new CommandLiteral("create"),
                        new CommandArgument("name")
                    ],
                    typeof(CreateTeamCommand),
                    arguments => new CreateTeamCommand(
                        arguments.Get<string>("name")),
                    CommandDescription("TeamCreate_Description", "创建队伍")),
                new CommandRoute(
                    [
                        new CommandLiteral("join"),
                        new CommandArgument(
                            "team",
                            CommandArgumentKind.Guid)
                    ],
                    typeof(RequestJoinTeamCommand),
                    arguments => new RequestJoinTeamCommand(
                        arguments.Get<Guid>("team")),
                    CommandDescription("TeamJoin_Description", "申请加入指定队伍")),
                new CommandRoute(
                    [new CommandLiteral("leave")],
                    typeof(LeaveTeamCommand),
                    _ => new LeaveTeamCommand(),
                    CommandDescription("TeamLeave_Description", "退出当前队伍"))
            ]);
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

    private static CommandResult ExecuteHelp(
        CommandContext context,
        ShowCommandHelpCommand command)
    {
        if (!string.IsNullOrWhiteSpace(command.CommandName))
        {
            return ExecuteHelpForCommand(context, command.CommandName);
        }

        var textAdapter = new TextCommandAdapter(context.Registry);
        var names = textAdapter.Entries
            .Where(entry =>
                entry.Command.Routes.Any(route =>
                    context.Registry.CanInvoke(
                        route.CommandType,
                        context.Principal)))
            .Select(entry => "/" + entry.Command.Name)
            .ToArray();
        return names.Length == 0
            ? CommandResult.LocalizedOk(
                "command.help",
                "HelpEmpty_Message",
                "当前没有可用指令。")
            : CommandResult.LocalizedOk(
                "command.help",
                "HelpHeading_Message",
                "可用指令：\n{0}",
                string.Join("\n", names.Select(item => $"- {item}")));
    }

    private static CommandResult ExecuteHelpForCommand(
        CommandContext context,
        string commandName)
    {
        var name = commandName.TrimStart('/');
        var textAdapter = new TextCommandAdapter(context.Registry);
        if (!textAdapter.TryFind(name, out var registered) || registered is null)
        {
            return CommandResult.LocalizedFail(
                "command.unknown",
                "HelpUnknown_Message",
                "未知指令：{0}。",
                name);
        }

        var routes = registered.Command.Routes
            .Where(route =>
                context.Registry.CanInvoke(
                    route.CommandType,
                    context.Principal))
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
            ? CommandResult.LocalizedFail(
                "command.forbidden",
                "HelpForbidden_Message",
                "你没有查看该指令的权限。")
            : CommandResult.LocalizedOk(
                "command.help.detail",
                "HelpDetail_Message",
                "{0}\n用法：\n{1}",
                registered.Command.Description.Resolve(),
                string.Join("\n", routes.Select(item => $"- {item}")));
    }

    private static CommandResult ExecuteTimeGet(
        CommandContext context,
        GetWorldTimeCommand command)
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        var time = context.Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        return CommandResult.LocalizedOk(
            "world.time",
            "CurrentTime_Message",
            "当前时间：{0}（第 {1} 天）",
            time.CalculateTimeOfDay().ToString("0.000"),
            time.Day.ToString("0.00"));
    }

    private static CommandResult ExecutePlayerList(
        CommandContext context,
        ListPlayersCommand command)
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        var players = GetPlayers(context.Project)
            .Select(player => new
            {
                player.Name,
                Online = player.Client is not null || player.IsMainPlayer
            })
            .OrderByDescending(player => player.Online)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (players.Length == 0)
        {
            return CommandResult.LocalizedOk(
                "player.list",
                "PlayerListEmpty_Message",
                "玩家列表为空。");
        }

        var onlineCount = players.Count(player => player.Online);
        var entries = players.Select(player =>
            $"{(player.Online ? "●" : "○")} {player.Name}");
        return CommandResult.LocalizedOk(
            "player.list",
            "PlayerListHeading_Message",
            "玩家 {0} 人（在线 {1}）：\n{2}",
            players.Length.ToString(),
            onlineCount.ToString(),
            string.Join("\n", entries.Select(item => $"- {item}")));
    }

    private static CommandResult ExecuteRunModeGet(
        CommandContext context,
        GetRunModeCommand command)
    {
        return CommandResult.LocalizedOk(
            "application.run_mode",
            "CurrentRunMode_Message",
            "当前运行模式：{0}。",
            FormatRunMode(RunMode.Value));
    }

    private static CommandResult ExecuteRunModeSet(
        CommandContext context,
        SetRunModeCommand command)
    {
        if (!Enum.IsDefined(command.TargetMode))
        {
            return CommandResult.LocalizedFail(
                "application.run_mode.invalid",
                "RunModeInvalid_Message",
                "不支持的运行模式。");
        }

        if (command.TargetMode == RunMode.Value && command.RestartSession == null)
        {
            return CommandResult.LocalizedOk(
                "application.run_mode.unchanged",
                "RunModeUnchanged_Message",
                "当前已经是 {0} 模式，无需重启。",
                FormatRunMode(command.TargetMode));
        }

        RunningSettingManager.SetRunMode(command.TargetMode);
        GameExitManager.RequestRestart(command.RestartSession);
        return CommandResult.LocalizedOk(
            "application.run_mode.restarting",
            "RunModeRestarting_Message",
            "运行模式已切换为 {0}，正在重启。",
            FormatRunMode(command.TargetMode));
    }

    private static CommandResult ExecuteApplicationRestart(
        CommandContext context,
        RestartApplicationCommand command)
    {
        GameExitManager.RequestRestart(command.RestartSession);
        return CommandResult.LocalizedOk(
            "application.restarting",
            "ApplicationRestarting_Message",
            "正在重新启动应用。");
    }

    private static CommandResult ExecuteInstanceSwitch(
        CommandContext context,
        SwitchInstanceCommand command)
    {
        var instanceId = command.InstanceId?.Trim() ?? string.Empty;
        if (string.Equals(
                instanceId,
                StarterInstanceManager.Current.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.unchanged",
                "InstanceUnchanged_Message",
                "当前已经在该实例中运行。");
        }

        if (!StarterInstanceManager.ListInstances().Contains(
                instanceId,
                StringComparer.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.not_found",
                "InstanceNotFound_Message",
                "找不到实例：{0}。",
                instanceId);
        }

        GameExitManager.RequestInstanceSwitch(instanceId);
        return CommandResult.LocalizedOk(
            "application.instance.switching",
            "InstanceSwitching_Message",
            "正在切换到实例 {0}。",
            instanceId);
    }

    private static CommandResult ExecuteApplicationExit(
        CommandContext context,
        ExitApplicationCommand command)
    {
        GameExitManager.RequestExit();
        return CommandResult.LocalizedOk(
            "application.exiting",
            "ApplicationExiting_Message",
            "正在退出应用。");
    }

    private static CommandResult ExecuteInstanceCreate(
        CommandContext context,
        CreateInstanceCommand command)
    {
        var instanceId = command.InstanceId?.Trim() ?? string.Empty;
        try
        {
            StarterInstanceManager.CreateInstance(instanceId);
        }
        catch (ArgumentException)
        {
            return CommandResult.LocalizedFail(
                "application.instance.invalid_id",
                "InstanceInvalidId_Message",
                "实例 ID 只能包含英文字母、数字、'-' 和 '_'。");
        }
        catch (InvalidOperationException)
        {
            return CommandResult.LocalizedFail(
                "application.instance.already_exists",
                "InstanceAlreadyExists_Message",
                "实例已存在：{0}。",
                instanceId);
        }

        return CommandResult.LocalizedOk(
            "application.instance.created",
            "InstanceCreated_Message",
            "已创建实例 {0}。",
            instanceId);
    }

    private static CommandResult ExecuteInstanceDelete(
        CommandContext context,
        DeleteInstanceCommand command)
    {
        var instanceId = command.InstanceId?.Trim() ?? string.Empty;
        if (string.Equals(instanceId, StarterInstanceManager.Current.Id, StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.current_delete_forbidden",
                "InstanceCurrentDeleteForbidden_Message",
                "不能删除当前实例。");
        }

        if (!StarterInstanceManager.ListInstances().Contains(instanceId, StringComparer.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.not_found",
                "InstanceNotFound_Message",
                "找不到实例：{0}。",
                instanceId);
        }

        if (StarterInstanceManager.IsInstanceRunning(instanceId))
        {
            return CommandResult.LocalizedFail(
                "application.instance.running_delete_forbidden",
                "InstanceRunningDeleteForbidden_Message",
                "不能删除正在运行的实例：{0}。",
                instanceId);
        }

        StarterInstanceManager.DeleteInstance(instanceId);
        return CommandResult.LocalizedOk(
            "application.instance.deleted",
            "InstanceDeleted_Message",
            "已删除实例 {0}。",
            instanceId);
    }

    private static CommandResult ExecuteInstanceClone(
        CommandContext context,
        CloneInstanceCommand command)
    {
        var sourceInstanceId = command.SourceInstanceId?.Trim() ?? string.Empty;
        var targetInstanceId = command.TargetInstanceId?.Trim() ?? string.Empty;
        try
        {
            StarterInstanceManager.ValidateInstanceId(targetInstanceId);
        }
        catch (ArgumentException)
        {
            return CommandResult.LocalizedFail(
                "application.instance.invalid_id",
                "InstanceInvalidId_Message",
                "实例 ID 只能包含英文字母、数字、'-' 和 '_'。");
        }

        if (!StarterInstanceManager.ListInstances().Contains(sourceInstanceId, StringComparer.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.not_found",
                "InstanceNotFound_Message",
                "找不到实例：{0}。",
                sourceInstanceId);
        }

        if (!StarterInstanceManager.CanCloneInstance(sourceInstanceId))
        {
            return CommandResult.LocalizedFail(
                "application.instance.running_clone_forbidden",
                "InstanceRunningCloneForbidden_Message",
                "不能克隆其他进程正在使用的实例：{0}。",
                sourceInstanceId);
        }

        if (StarterInstanceManager.ListInstances().Contains(targetInstanceId, StringComparer.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedFail(
                "application.instance.already_exists",
                "InstanceAlreadyExists_Message",
                "实例已存在：{0}。",
                targetInstanceId);
        }

        try
        {
            StarterInstanceManager.CloneInstance(sourceInstanceId, targetInstanceId);
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to clone starter instance '{sourceInstanceId}' to '{targetInstanceId}': {ex}");
            return CommandResult.LocalizedFail(
                "application.instance.clone_failed",
                "InstanceCloneFailed_Message",
                "克隆实例失败。");
        }

        return CommandResult.LocalizedOk(
            "application.instance.cloned",
            "InstanceCloned_Message",
            "已将实例 {0} 克隆为 {1}。",
            sourceInstanceId,
            targetInstanceId);
    }

    private static CommandResult ExecuteLanguageGet(
        CommandContext context,
        GetLanguageCommand command)
    {
        var languageType = CurrentLanguageType();
        return CommandResult.LocalizedOk(
            "application.language",
            "CurrentLanguage_Message",
            "当前语言：{0}（{1}）。",
            languageType,
            LanguageManager.GetLanguageDisplayName(languageType));
    }

    private static CommandResult ExecuteLanguageSet(
        CommandContext context,
        SetLanguageCommand command)
    {
        var languageType = LanguageManager.LanguageTypes.FirstOrDefault(type =>
            type.Equals(command.LanguageType, StringComparison.OrdinalIgnoreCase));
        if (languageType is null)
        {
            return CommandResult.LocalizedFail(
                "application.language.invalid",
                "LanguageInvalid_Message",
                "不支持的语言：{0}。可用语言：{1}。",
                command.LanguageType,
                string.Join(", ", LanguageManager.LanguageTypes));
        }

        var currentLanguage = CurrentLanguageType();
        if (languageType.Equals(currentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return CommandResult.LocalizedOk(
                "application.language.unchanged",
                "LanguageUnchanged_Message",
                "当前已经使用 {0}，无需切换。",
                LanguageManager.GetLanguageDisplayName(languageType));
        }

        if (CurrentModRuntime.Value is not { } runtime)
        {
            return CommandResult.LocalizedFail(
                "application.language.unavailable",
                "CommandUnavailable_Message",
                "命令系统尚未就绪。");
        }

        runtime.InitializeLanguage(languageType);
        if (RunMode.Value is RunModeType.Gui)
        {
            runtime.InitializeCraftingRecipes();
        }

        SettingsManager.SaveSettings();
        return CommandResult.LocalizedOk(
            "application.language.changed",
            "LanguageChanged_Message",
            "语言已切换为 {0}（{1}）。",
            languageType,
            LanguageManager.GetLanguageDisplayName(languageType));
    }

    private static string CurrentLanguageType()
    {
        return LanguageManager.CurrentLanguage;
    }

    private static RunModeType ParseRunMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "gui" => RunModeType.Gui,
            "headless" => RunModeType.HeadlessServer,
            _ => throw new InvalidOperationException($"Unsupported run mode {value}.")
        };
    }

    private static string FormatRunMode(RunModeType runMode)
    {
        return runMode switch
        {
            RunModeType.Gui => "GUI",
            RunModeType.HeadlessServer => "HeadlessServer",
            _ => runMode.ToString()
        };
    }

    private static float ParseSeasonProgress(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "start" => 0f,
            "middle" => 0.5f,
            "end" => 0.999f,
            _ => throw new InvalidOperationException(
                $"Unsupported season progress {value}.")
        };
    }

    private static bool ParseToggle(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "enable" => true,
            "disable" => false,
            _ => throw new InvalidOperationException(
                $"Unsupported toggle value {value}.")
        };
    }

    private static IEnumerable<CommandArgumentSuggestion> ToggleSuggestions(
        string enableKey,
        string enableFallback,
        string disableKey,
        string disableFallback)
    {
        return
        [
            new CommandArgumentSuggestion(
                "enable",
                CommandDescription(enableKey, enableFallback)),
            new CommandArgumentSuggestion(
                "disable",
                CommandDescription(disableKey, disableFallback))
        ];
    }

    private static IEnumerable<CommandArgumentSuggestion> SeasonSuggestions()
    {
        return
        [
            new CommandArgumentSuggestion(
                "summer",
                CommandDescription("SeasonSummer_Description", "夏季")),
            new CommandArgumentSuggestion(
                "autumn",
                CommandDescription("SeasonAutumn_Description", "秋季")),
            new CommandArgumentSuggestion(
                "winter",
                CommandDescription("SeasonWinter_Description", "冬季")),
            new CommandArgumentSuggestion(
                "spring",
                CommandDescription("SeasonSpring_Description", "春季"))
        ];
    }

    private static IEnumerable<CommandArgumentSuggestion> TimeSuggestions()
    {
        return
        [
            new CommandArgumentSuggestion(
                "sunrise",
                new LocalizedText("TimeOfDayMode", "Sunrise", "黎明")),
            new CommandArgumentSuggestion(
                "day",
                new LocalizedText("TimeOfDayMode", "Day", "中午")),
            new CommandArgumentSuggestion(
                "sunset",
                new LocalizedText("TimeOfDayMode", "Sunset", "黄昏")),
            new CommandArgumentSuggestion(
                "night",
                new LocalizedText("TimeOfDayMode", "Night", "午夜"))
        ];
    }

    private static IEnumerable<CommandArgumentSuggestion> SeasonProgressSuggestions()
    {
        return
        [
            new CommandArgumentSuggestion(
                "start",
                CommandDescription("SeasonStart_Description", "季节初期")),
            new CommandArgumentSuggestion(
                "middle",
                CommandDescription("SeasonMiddle_Description", "季节中期")),
            new CommandArgumentSuggestion(
                "end",
                CommandDescription("SeasonEnd_Description", "季节末期"))
        ];
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestLanguages(
        CommandSuggestionContext context)
    {
        return LanguageManager.LanguageTypes.Select(languageType =>
            new CommandArgumentSuggestion(
                languageType,
                LocalizedText.Literal(
                    LanguageManager.GetLanguageDisplayName(languageType))));
    }

    private static LocalizedText CommandDescription(
        string key,
        string fallback)
    {
        return new LocalizedText("Commands", key, fallback);
    }

    private static CommandResult ExecutePermissionListSelf(
        CommandContext context,
        ListOwnPermissionsCommand command)
    {
        if (context.Principal.Player is null)
        {
            return CommandResult.LocalizedFail(
                "permission.player_required",
                "PermissionConsolePlayerRequired_Message",
                "服务器控制台请指定玩家：permission list <player>。");
        }

        return FormatPermissionList(context.Principal.Player);
    }

    private static CommandResult ExecuteAuthHelp(
        CommandContext context,
        ShowServerAuthHelpCommand command)
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        return ServerAdministrationBootstrap.IsClaimed(context.Project)
            ? CommandResult.LocalizedOk(
                "auth.claimed",
                "AuthClaimed_Message",
                "服务器管理员已经完成首次认领。")
            : context.Principal.Is(CommandPrincipalKind.ServerOperator)
                ? CommandResult.LocalizedOk(
                    "auth.unclaimed",
                    "AuthUnclaimedConsole_Message",
                    "服务器尚未认领。\n使用 auth code 查看认领码，玩家上线后执行 /auth claim <认领码>。")
                : CommandResult.LocalizedOk(
                    "auth.unclaimed",
                    "AuthUnclaimedPlayer_Message",
                    "服务器尚未认领。\n请输入 /auth claim <认领码> 完成首次管理员授权。");
    }

    private static CommandResult ExecuteAuthClaim(
        CommandContext context,
        ClaimServerAdministrationCommand command)
    {
        if (!EnsureServer(out var failure))
        {
            return failure;
        }

        if (context.Project is null)
        {
            return NoWorld();
        }

        if (context.Principal.Player is not { } player)
        {
            return CommandResult.LocalizedFail(
                "auth.player_required",
                "AuthPlayerRequired_Message",
                "认领必须由已连接服务器的在线玩家执行。");
        }

        var result = ServerAdministrationBootstrap.TryClaim(
            context.Project,
            player,
            command.Code);
        if (!result.Success)
        {
            return new CommandResult(
                false,
                result.Code,
                result.Message,
                MessageKey: result.MessageKey,
                MessageArguments: result.MessageArguments);
        }

        SynchronizePermissions(player);
        GameManager.SaveProject(
            waitForCompletion: false,
            showErrorDialog: RunMode.Value is RunModeType.Gui);
        return new CommandResult(
            true,
            result.Code,
            result.Message,
            MessageKey: result.MessageKey,
            MessageArguments: result.MessageArguments);
    }

    private static CommandResult ExecuteAuthStatus(
        CommandContext context,
        GetServerAuthStatusCommand command)
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        return ServerAdministrationBootstrap.IsClaimed(context.Project)
            ? CommandResult.LocalizedOk(
                "auth.claimed",
                "AuthClaimed_Message",
                "服务器管理员已经完成首次认领。")
            : CommandResult.LocalizedOk(
                "auth.unclaimed",
                "AuthUnclaimedStatus_Message",
                "服务器尚未认领，必须由在线玩家提交认领码。");
    }

    private static CommandResult ExecuteAuthCode(
        CommandContext context,
        GetServerAuthCodeCommand command)
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        return ServerAdministrationBootstrap.TryGetClaimCode(context.Project, out var code)
            ? CommandResult.LocalizedSensitiveOk(
                "auth.code",
                "AuthCode_Message",
                "认领码：{0}\n在线玩家执行 /auth claim {0}",
                code)
            : CommandResult.LocalizedFail(
                "auth.already_claimed",
                "AuthClaimed_Message",
                "服务器管理员已经完成首次认领。");
    }

    private static CommandResult ExecuteAuthRegenerate(
        CommandContext context,
        RegenerateServerAuthCodeCommand command)
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        return ServerAdministrationBootstrap.TryRegenerateClaimCode(context.Project, out var code)
            ? CommandResult.LocalizedSensitiveOk(
                "auth.regenerated",
                "AuthRegenerated_Message",
                "已重新生成认领码：{0}\n在线玩家执行 /auth claim {0}",
                code)
            : CommandResult.LocalizedFail(
                "auth.already_claimed",
                "AuthClaimed_Message",
                "服务器管理员已经完成首次认领。");
    }

    private static CommandResult ExecutePermissionHelp(
        CommandContext context,
        ShowPermissionHelpCommand command)
    {
        var players = GetPlayers(context.Project).Select(player => player.Name).ToArray();
        var nodes = context.Registry.Permissions.Definitions
            .Select(node => node.Id)
            .Where(node => context.Registry.Permissions.CanGrant(
                node,
                context.Principal,
                context.Project))
            .Select(node => node.ToString())
            .ToArray();
        return CommandResult.LocalizedOk(
            "permission.help",
            "PermissionHelp_Message",
            "授权用法：\n- /permission grant <player> <permission>（仅使用）\n- /permission delegate <player> <permission>（允许再授权）\n- /permission revoke <player> <permission>\n- /permission list [player]\n当前玩家：\n{0}\n可授权节点：\n{1}",
            FormatItems(players),
            FormatItems(nodes));
    }

    private static CommandResult ExecutePermissionPlayers(
        CommandContext context,
        ListPermissionPlayersCommand command)
    {
        var players = GetPlayers(context.Project).ToArray();
        return players.Length == 0
            ? CommandResult.LocalizedOk(
                "permission.players",
                "PermissionPlayersEmpty_Message",
                "当前没有可授权的在线玩家。")
            : CommandResult.LocalizedOk(
                "permission.players",
                "PermissionPlayersHeading_Message",
                "当前玩家：\n{0}",
                FormatItems(players.Select(player =>
                    $"{player.Name} ({player.PlayerGUID})")));
    }

    private static CommandResult ExecutePermissionNodes(
        CommandContext context,
        ListPermissionNodesCommand command)
    {
        var nodes = context.Registry.Permissions.Definitions
            .Select(node => node.Id)
            .Where(node => context.Registry.Permissions.CanGrant(
                node,
                context.Principal,
                context.Project))
            .Select(node => node.ToString())
            .ToArray();
        return nodes.Length == 0
            ? CommandResult.LocalizedOk(
                "permission.nodes",
                "PermissionNodesEmpty_Message",
                "当前没有可再授权的权限节点。")
            : CommandResult.LocalizedOk(
                "permission.nodes",
                "PermissionNodesHeading_Message",
                "可授权节点：\n{0}",
                FormatItems(nodes));
    }

    private static CommandResult ExecutePermissionListPlayer(
        CommandContext context,
        ListPlayerPermissionsCommand command)
    {
        return TryFindPlayer(context, command.Player, out var player, out var failure)
            ? FormatPermissionList(player!)
            : failure!;
    }

    private static CommandResult ExecutePermissionGrant(
        CommandContext context,
        GrantPlayerPermissionCommand command)
    {
        if (!EnsureServer(out var failure))
        {
            return failure;
        }

        if (!TryParsePermission(command.Permission, out var permission))
        {
            return CommandResult.LocalizedFail(
                "permission.invalid",
                "PermissionInvalid_Message",
                "权限节点格式无效。");
        }

        if (!context.Registry.Permissions.CanGrant(
                permission,
                context.Principal,
                context.Project,
                command.CanDelegate))
        {
            return CommandResult.LocalizedFail(
                "permission.cannot_delegate",
                "PermissionCannotGrant_Message",
                "权限节点 {0} 不可授权，或你没有对应的管理范围。",
                permission.ToString());
        }

        if (!TryFindPlayer(context, command.Player, out var player, out failure))
        {
            return failure!;
        }

        var changed = player!.CommandPermissions.Grant(permission, command.CanDelegate);
        if (changed)
        {
            SynchronizePermissions(player);
        }

        if (!changed)
        {
            return CommandResult.LocalizedOk(
                "permission.unchanged",
                "PermissionUnchanged_Message",
                "{0} 已拥有相同或更高范围的 {1} 权限。",
                player.Name,
                permission.ToString());
        }

        return CommandResult.LocalizedOk(
            "permission.granted",
            command.CanDelegate
                ? "PermissionGrantedDelegate_Message"
                : "PermissionGrantedUse_Message",
            command.CanDelegate
                ? "已授予 {0} 权限 {1}（允许再授权）。"
                : "已授予 {0} 权限 {1}（仅允许使用）。",
            player.Name,
            permission.ToString());
    }

    private static CommandResult ExecutePermissionRevoke(
        CommandContext context,
        RevokePlayerPermissionCommand command)
    {
        if (!EnsureServer(out var failure))
        {
            return failure;
        }

        if (!TryParsePermission(command.Permission, out var permission))
        {
            return CommandResult.LocalizedFail(
                "permission.invalid",
                "PermissionInvalid_Message",
                "权限节点格式无效。");
        }

        if (!context.Registry.Permissions.CanGrant(
                permission,
                context.Principal,
                context.Project))
        {
            return CommandResult.LocalizedFail(
                "permission.cannot_delegate",
                "PermissionCannotRevoke_Message",
                "权限节点 {0} 不可撤销，或你没有对应的管理范围。",
                permission.ToString());
        }

        if (!TryFindPlayer(context, command.Player, out var player, out failure))
        {
            return failure!;
        }

        if (!player!.CommandPermissions.Revoke(permission))
        {
            return CommandResult.LocalizedFail(
                "permission.not_found",
                "PermissionNotHeld_Message",
                "{0} 没有直接持有权限 {1}。",
                player.Name,
                permission.ToString());
        }

        SynchronizePermissions(player);
        return CommandResult.LocalizedOk(
            "permission.revoked",
            "PermissionRevoked_Message",
            "已撤销 {0} 的权限 {1}。",
            player.Name,
            permission.ToString());
    }

    private static CommandResult ExecuteTimeSet(
        CommandContext context,
        SetWorldTimeCommand command)
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        var time = context.Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        var value = command.Preset;
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

        return CommandResult.LocalizedPublicOk(
            "world.time.changed",
            "TimeChanged_Message",
            "已将世界时间设置为 {0}。",
            value);
    }

    private static CommandResult ExecuteTimeAdvance(
        CommandContext context,
        AdvanceWorldTimeCommand command)
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        var time = context.Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        var targets = new[]
        {
            ("sunrise", time.MidDawn),
            ("day", time.Midday),
            ("sunset", time.MidDusk),
            ("night", time.Midnight)
        };
        var next = targets
            .Select(target => (
                Name: target.Item1,
                Offset: IntervalUtils.Interval(time.TimeOfDay, target.Item2)))
            .MinBy(target => target.Offset);
        time.TimeOfDayOffset += next.Offset;
        if (CommonLib.WorkType is WorkType.Server)
        {
            CommonLib.Net.QueuePackage(
                new SubsystemTimePackage(
                    time.SubsystemGameInfo.TotalElapsedGameTime,
                    time.TimeOfDayOffset));
        }

        return CommandResult.LocalizedPublicOk(
            "world.time.advanced",
            "TimeAdvanced_Message",
            "已将世界时间推进到 {0}。",
            next.Name);
    }

    private static CommandResult ExecuteStop(
        CommandContext context,
        StopServerCommand command)
    {
        if (RunMode.Value is not RunModeType.HeadlessServer ||
            CommonLib.WorkType is not WorkType.Server)
        {
            return CommandResult.LocalizedFail(
                "server.not_headless",
                "ServerNotHeadless_Message",
                "当前进程不是 Headless 服务端。");
        }

        HeadlessEntry.RequestStop();
        return CommandResult.LocalizedOk(
            "server.stopping",
            "ServerStopping_Message",
            "服务端正在保存并停止。");
    }

    private static CommandResult NoWorld()
    {
        return CommandResult.LocalizedFail(
            "command.no_world",
            "NoWorld_Message",
            "当前没有加载世界。");
    }

    private static bool EnsureServer(out CommandResult failure)
    {
        if (CommonLib.WorkType is WorkType.Server)
        {
            failure = null!;
            return true;
        }

        failure = CommandResult.LocalizedFail(
            "permission.server_only",
            "ServerOnly_Message",
            "该操作只能在服务器上执行。");
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
            failure = NoWorld();
            return false;
        }

        player = FindPlayer(context.Project, value);
        if (player is not null)
        {
            failure = null;
            return true;
        }

        failure = CommandResult.LocalizedFail(
            "permission.player_not_found",
            "PlayerNotFound_Message",
            "找不到玩家：{0}。",
            value);
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

    private static bool TryParsePermission(
        string value,
        out ResourceId permission)
    {
        var separator = value.IndexOf(':');
        if (separator <= 0 || separator == value.Length - 1 ||
            value.IndexOf(':', separator + 1) >= 0)
        {
            permission = default;
            return false;
        }

        try
        {
            permission = new ResourceId(
                new ModId(value[..separator]),
                value[(separator + 1)..]);
            return true;
        }
        catch (ArgumentException)
        {
            permission = default;
            return false;
        }
    }

    private static IEnumerable<PlayerData> GetPlayers(Project? project)
    {
        return project?.FindSubsystem<SubsystemPlayers>(true)?.PlayersData ?? [];
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestPlayers(
        CommandSuggestionContext context)
    {
        return GetPlayers(context.Project).Select(player =>
            new CommandArgumentSuggestion(
                player.Name,
                LocalizedText.Literal(player.PlayerGUID.ToString())));
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestCommands(
        CommandSuggestionContext context)
    {
        var textAdapter = new TextCommandAdapter(context.Registry);
        return textAdapter.Entries
            .Where(entry =>
                entry.Command.Routes.Any(route =>
                    context.Registry.CanInvoke(
                        route.CommandType,
                        context.Principal)))
            .Select(entry => new CommandArgumentSuggestion(
                entry.Command.Name,
                entry.Command.Description));
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestDelegablePermissionNodes(
        CommandSuggestionContext context)
    {
        return context.Registry.Permissions.Definitions
            .Select(node => node.Id)
            .Where(node => context.Registry.Permissions.CanGrant(
                node,
                context.Principal,
                context.Project,
                canDelegate: true))
            .Select(node => new CommandArgumentSuggestion(
                node.ToString(),
                CommandDescription(
                    "PermissionNodeSuggestion_Description",
                    "可授权权限节点")));
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
            .Where(grant => context.Registry.Permissions.CanGrant(
                grant.Permission,
                context.Principal,
                context.Project))
            .Select(grant => new CommandArgumentSuggestion(
                grant.Permission.ToString(),
                grant.CanDelegate
                    ? CommandDescription(
                        "PermissionCurrentDelegate_Description",
                        "当前为可再授权")
                    : CommandDescription(
                        "PermissionCurrentUse_Description",
                        "当前为仅使用")));
    }

    private static CommandResult FormatPermissionList(PlayerData player)
    {
        var grants = player.CommandPermissions.Grants;
        if (grants.Count == 0)
        {
            return CommandResult.LocalizedOk(
                "permission.list",
                "PermissionListEmpty_Message",
                "{0} 当前没有任何指令权限。",
                player.Name);
        }

        var values = grants.Select(grant =>
            $"{grant.Permission} [{(grant.CanDelegate ? "delegate" : "use")}]");
        return CommandResult.LocalizedOk(
            "permission.list",
            "PermissionListHeading_Message",
            "{0} 的指令权限：\n{1}",
            player.Name,
            FormatItems(values));
    }

    private static string FormatItems(IEnumerable<string> items)
    {
        var values = items
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        return values.Length == 0
            ? "-"
            : string.Join("\n", values.Select(item => $"- {item}"));
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

    private sealed class DirectRegistration(
        CommandRegistry registry,
        ModId owner) : IModCommands
    {
        public IModCommandAdapters Adapters { get; } =
            new DirectAdapterRegistration(registry.Adapters, owner);

        public IModCommandPermissions Permissions { get; } =
            new DirectPermissionRegistration(registry.Permissions, owner);

        public IDisposable Register<TCommand>(
            ResourceId id,
            CommandDefinition<TCommand> definition)
            where TCommand : IGameCommand
        {
            return registry.Register(owner, id, definition);
        }
    }

    private sealed class DirectPermissionRegistration(
        CommandPermissionRegistry permissions,
        ModId owner) : IModCommandPermissions
    {
        public IDisposable Register(
            ResourceId id,
            CommandPermissionDefinition definition)
        {
            return permissions.Register(owner, id, definition);
        }
    }

    private sealed class DirectAdapterRegistration(
        CommandAdapterRegistry adapters,
        ModId owner) : IModCommandAdapters
    {
        public IDisposable Register<TBinding>(
            ResourceId id,
            TBinding binding)
            where TBinding : class, ICommandAdapterBinding
        {
            return adapters.Register(owner, id, binding);
        }

        public IReadOnlyList<RegisteredCommandAdapter<TBinding>> Get<TBinding>()
            where TBinding : class, ICommandAdapterBinding
        {
            return adapters.Get<TBinding>();
        }

        public bool TryGet<TBinding>(ResourceId id, out TBinding? binding)
            where TBinding : class, ICommandAdapterBinding
        {
            return adapters.TryGet(id, out binding);
        }
    }
}
