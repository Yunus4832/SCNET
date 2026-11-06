using System.Text.Json.Nodes;

using EntitySystem.Core;

using Game.Commands.Http;
using Game.Commands.Text;

namespace Game.Commands.BuiltIn;

public static class BuiltInWorldCommands
{
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
        var teleportSelf = new ResourceId(owner, "player.teleport.self");
        var teleportOthers = new ResourceId(owner, "player.teleport.others");
        var publicMark = new ResourceId(owner, "world.mark.manage");

        var p2pManage = new ResourceId(owner, "server.p2p.manage");
        PlayerModerationCommandHandlers.Register(commands, owner);

        RegisterPermission(
            commands,
            permissionGrant,
            CommandDomain.Server,
            PermissionGrantPolicy.NonGrantable,
            description: CommandDescription("PermissionGrant_Description", "授予主体命令权限"),
            implicitGrant: (principal, _) =>
                principal.DelegablePermissions.Count > 0 ||
                principal.HasPermission(permissionManageStandard)
        );
        RegisterPermission(
            commands,
            permissionManageStandard,
            CommandDomain.Server,
            description: CommandDescription("PermissionManageStandard_Description", "管理标准命令权限"),
            managesStandardPermissions: true
        );
        RegisterCreativePermission(commands, worldTimeSet, CommandDescription("TimeSet_Description", "设置世界时间"));
        RegisterCreativePermission(
            commands,
            worldPrecipitationSet,
            CommandDescription("WeatherRain_Description", "开启或停止降水")
        );
        RegisterCreativePermission(commands, worldFogSet, CommandDescription("WeatherFog_Description", "开启或关闭雾气"));
        RegisterCreativePermission(
            commands,
            worldLightningTrigger,
            CommandDescription("LightningTarget_Description", "在目标附近触发闪电")
        );
        RegisterCreativePermission(
            commands,
            worldSeasonSet,
            CommandDescription("SeasonSet_Description", "调整季节，环境将逐步更新")
        );
        RegisterCreativePermission(commands, teleportSelf, CommandDescription("TeleportSelf_Description", "传送自己"));
        RegisterPermission(
            commands,
            teleportOthers,
            CommandDomain.World,
            description: CommandDescription("TeleportOther_Description", "传送指定玩家")
        );
        RegisterCreativePermission(commands, publicMark, CommandDescription("MarkPublic_Description", "保存公共标记"));
        commands.Register(
            new ResourceId(owner, "player/mark/private"),
            new CommandDefinition<MarkPrivatePositionCommand>(
                TeleportCommandHandlers.MarkPrivate,
                CommandDomain.World,
                CommandDescription("MarkPrivate_Description", "保存个人标记"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "world/mark/public"),
            new CommandDefinition<MarkPublicPositionCommand>(
                TeleportCommandHandlers.MarkPublic,
                CommandDomain.World,
                CommandDescription("MarkPublic_Description", "保存公共标记"),
                publicMark,
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/teleport/mark"),
            new CommandDefinition<TeleportMarkCommand>(
                TeleportCommandHandlers.MarkTeleport,
                CommandDomain.World,
                CommandDescription("TeleportMark_Description", "传送到命名标记"),
                teleportSelf,
                allowedPrincipals: CommandPrincipalKinds.Player,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/teleport/other_mark"),
            new CommandDefinition<TeleportPlayerMarkCommand>(
                TeleportCommandHandlers.PlayerMarkTeleport,
                CommandDomain.World,
                CommandDescription("TeleportOtherMark_Description", "传送指定玩家到标记"),
                teleportOthers,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/teleport/self"),
            new CommandDefinition<TeleportSelfCommand>(
                TeleportCommandHandlers.Self,
                CommandDomain.World,
                CommandDescription("TeleportSelf_Description", "传送自己"),
                teleportSelf,
                allowedPrincipals: CommandPrincipalKinds.Player | CommandPrincipalKinds.System,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/teleport/other"),
            new CommandDefinition<TeleportPlayerCommand>(
                TeleportCommandHandlers.Player,
                CommandDomain.World,
                CommandDescription("TeleportOther_Description", "传送指定玩家"),
                teleportOthers,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        RegisterPermission(
            commands,
            serverStop,
            CommandDomain.Server,
            PermissionGrantPolicy.NonGrantable,
            description: CommandDescription("StopAction_Description", "停止服务器")
        );
        RegisterPermission(
            commands,
            serverAuthManage,
            CommandDomain.Server,
            PermissionGrantPolicy.NonGrantable,
            description: CommandDescription("Auth_Description", "认领服务器管理员身份")
        );
        RegisterPermission(
            commands,
            playerProfileManage,
            CommandDomain.Server,
            PermissionGrantPolicy.NonGrantable,
            description: CommandDescription("PlayerProfileServer_Description", "由服务器更新角色资料")
        );
        RegisterPermission(
            commands,
            p2pManage,
            CommandDomain.Server,
            description: CommandDescription("P2PManage_Description", "管理 P2P 房间")
        );
        commands.Register(
            new ResourceId(owner, "server/p2p/start"),
            new CommandDefinition<StartP2PCommand>(
                P2PCommandHandlers.Start,
                CommandDomain.Server,
                CommandDescription("P2PStart_Description", "开启 P2P 房间"),
                p2pManage,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "server/p2p/stop"),
            new CommandDefinition<StopP2PCommand>(
                P2PCommandHandlers.Stop,
                CommandDomain.Server,
                CommandDescription("P2PStop_Description", "停止 P2P 房间"),
                p2pManage,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "server/p2p/status"),
            new CommandDefinition<GetP2PStatusCommand>(
                P2PCommandHandlers.Status,
                CommandDomain.Server,
                CommandDescription("P2PStatus_Description", "查询 P2P 房间状态"),
                p2pManage,
                supportsRemoteInvocation: true
            )
        );

        commands.Register(
            new ResourceId(owner, "commands/catalog"),
            new CommandDefinition<GetCommandCatalogCommand>(
                (context, _) => CommandResult.SilentOk() with
                {
                    Data = new JsonObject
                    {
                        ["commands"] = new JsonArray(
                            context.Registry.Definitions
                                .Where(entry => entry.Definition.Domain is not CommandDomain.Application &&
                                                entry.Definition.CanExecuteHere(context.Project) &&
                                                entry.Definition.IsPotentiallyAuthorized(
                                                    context.Registry.Permissions,
                                                    context.Principal,
                                                    context.Project
                                                )
                                )
                                .Select(entry => (JsonNode?)JsonValue.Create(entry.Id.ToString())).ToArray()
                        )
                    }
                },
                CommandDomain.World,
                supportsRemoteInvocation: true
            )
        );

        commands.Register(
            new ResourceId(owner, "help"),
            new CommandDefinition<ShowCommandHelpCommand>(
                ExecuteHelp,
                CommandDomain.World,
                CommandDescription("Help_Description", "显示可用指令"),
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "world/time/get"),
            new CommandDefinition<GetWorldTimeCommand>(
                ExecuteTimeGet,
                CommandDomain.World,
                CommandDescription("TimeGet_Description", "显示当前世界时间"),
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "world/time/set"),
            new CommandDefinition<SetWorldTimeCommand>(
                ExecuteTimeSet,
                CommandDomain.World,
                CommandDescription("TimeSet_Description", "设置世界时间"),
                worldTimeSet,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "world/time/advance"),
            new CommandDefinition<AdvanceWorldTimeCommand>(
                ExecuteTimeAdvance,
                CommandDomain.World,
                CommandDescription("TimeAdvance_Description", "前进到下一个时间节点"),
                worldTimeSet,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "world/weather/precipitation/set"),
            new CommandDefinition<SetPrecipitationCommand>(
                WorldControlCommandHandlers.SetPrecipitation,
                CommandDomain.World,
                CommandDescription("WeatherRain_Description", "开启或停止降水"),
                worldPrecipitationSet,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "world/weather/fog/set"),
            new CommandDefinition<SetFogCommand>(
                WorldControlCommandHandlers.SetFog,
                CommandDomain.World,
                CommandDescription("WeatherFog_Description", "开启或关闭雾气"),
                worldFogSet,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "world/weather/lightning/trigger_player"),
            new CommandDefinition<TriggerPlayerLightningCommand>(
                WorldControlCommandHandlers.TriggerPlayerLightning,
                CommandDomain.World,
                CommandDescription(
                    "LightningPlayer_Description",
                    "在玩家注视位置附近触发闪电"
                ),
                worldLightningTrigger,
                allowedPrincipals: CommandPrincipalKinds.Player,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "world/weather/lightning/trigger"),
            new CommandDefinition<TriggerLightningCommand>(
                WorldControlCommandHandlers.TriggerLightning,
                CommandDomain.World,
                CommandDescription("LightningTarget_Description", "在目标附近触发闪电"),
                worldLightningTrigger,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "world/season/set"),
            new CommandDefinition<SetSeasonCommand>(
                WorldControlCommandHandlers.SetSeason,
                CommandDomain.World,
                CommandDescription("SeasonSet_Description", "调整季节，环境将逐步更新"),
                worldSeasonSet,
                panelBehavior: CommandPanelBehavior.CloseOnSuccess,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/list"),
            new CommandDefinition<ListPlayersCommand>(
                ExecutePlayerList,
                CommandDomain.World,
                CommandDescription("Players_Description", "列出已知角色及其在线状态"),
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "server/stop"),
            new CommandDefinition<StopServerCommand>(
                ExecuteStop,
                CommandDomain.Server,
                CommandDescription("StopAction_Description", "停止服务器"),
                serverStop,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "server/auth/help"),
            new CommandDefinition<ShowServerAuthHelpCommand>(
                ExecuteAuthHelp,
                CommandDomain.Server,
                CommandDescription("AuthHelp_Description", "显示服务器认领帮助"),
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "server/auth/claim"),
            new CommandDefinition<ClaimServerAdministrationCommand>(
                ExecuteAuthClaim,
                CommandDomain.Server,
                CommandDescription(
                    "AuthClaim_Description",
                    "使用认领码初始化在线玩家的管理权限"
                ),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "server/auth/status"),
            new CommandDefinition<GetServerAuthStatusCommand>(
                ExecuteAuthStatus,
                CommandDomain.Server,
                CommandDescription("AuthStatus_Description", "查看服务器认领状态"),
                serverAuthManage,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "server/auth/code"),
            new CommandDefinition<GetServerAuthCodeCommand>(
                ExecuteAuthCode,
                CommandDomain.Server,
                CommandDescription("AuthCode_Description", "显示当前服务器认领码"),
                serverAuthManage,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "server/auth/regenerate"),
            new CommandDefinition<RegenerateServerAuthCodeCommand>(
                ExecuteAuthRegenerate,
                CommandDomain.Server,
                CommandDescription("AuthRegenerate_Description", "重新生成服务器认领码"),
                serverAuthManage,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "permission/help"),
            new CommandDefinition<ShowPermissionHelpCommand>(
                ExecutePermissionHelp,
                CommandDomain.Server,
                CommandDescription("PermissionHelp_Description", "显示授权命令帮助"),
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "permission/players"),
            new CommandDefinition<ListPermissionPlayersCommand>(
                ExecutePermissionPlayers,
                CommandDomain.Server,
                CommandDescription("PermissionPlayers_Description", "列出当前可授权主体"),
                permissionGrant,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "permission/nodes"),
            new CommandDefinition<ListPermissionNodesCommand>(
                ExecutePermissionNodes,
                CommandDomain.Server,
                CommandDescription("PermissionNodes_Description", "列出当前可授权权限节点"),
                permissionGrant,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "permission/list_self"),
            new CommandDefinition<ListOwnPermissionsCommand>(
                ExecutePermissionListSelf,
                CommandDomain.Server,
                CommandDescription("PermissionListSelf_Description", "查看自己的命令权限"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "permission/list_player"),
            new CommandDefinition<ListPlayerPermissionsCommand>(
                ExecutePermissionListPlayer,
                CommandDomain.Server,
                CommandDescription(
                    "PermissionListPlayer_Description",
                    "查看指定主体的命令权限"
                ),
                permissionGrant,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "permission/grant"),
            new CommandDefinition<GrantPlayerPermissionCommand>(
                ExecutePermissionGrant,
                CommandDomain.Server,
                CommandDescription("PermissionGrant_Description", "授予主体命令权限"),
                permissionGrant,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "permission/revoke"),
            new CommandDefinition<RevokePlayerPermissionCommand>(
                ExecutePermissionRevoke,
                CommandDomain.Server,
                CommandDescription("PermissionRevoke_Description", "撤销主体命令权限"),
                permissionGrant,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/profile/update_self"),
            new CommandDefinition<UpdateOwnPlayerProfileCommand>(
                PlayerAndMessageCommandHandlers.UpdateOwnPlayerProfile,
                CommandDomain.World,
                CommandDescription(
                    "PlayerProfileSelf_Description",
                    "更新自己的角色资料"
                ),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "player/profile/update"),
            new CommandDefinition<UpdatePlayerProfileCommand>(
                PlayerAndMessageCommandHandlers.UpdatePlayerProfile,
                CommandDomain.Server,
                CommandDescription(
                    "PlayerProfileServer_Description",
                    "由服务器更新角色资料"
                ),
                playerProfileManage,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "chat/send"),
            new CommandDefinition<SendChatMessageCommand>(
                PlayerAndMessageCommandHandlers.SendChatMessage,
                CommandDomain.World,
                CommandDescription("ChatSend_Description", "发送聊天消息"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "team/create"),
            new CommandDefinition<CreateTeamCommand>(
                TeamCommandHandlers.CreateTeam,
                CommandDomain.World,
                CommandDescription("TeamCreate_Description", "创建队伍"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "team/request_join"),
            new CommandDefinition<RequestJoinTeamCommand>(
                TeamCommandHandlers.RequestJoin,
                CommandDomain.World,
                CommandDescription("TeamJoin_Description", "申请加入队伍"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "team/invite"),
            new CommandDefinition<InvitePlayerToTeamCommand>(
                TeamCommandHandlers.InvitePlayer,
                CommandDomain.World,
                CommandDescription("TeamInvite_Description", "邀请玩家加入队伍"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "team/respond"),
            new CommandDefinition<RespondTeamRequestCommand>(
                TeamCommandHandlers.Respond,
                CommandDomain.World,
                CommandDescription("TeamRespond_Description", "响应队伍请求"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );
        commands.Register(
            new ResourceId(owner, "team/leave"),
            new CommandDefinition<LeaveTeamCommand>(
                TeamCommandHandlers.LeaveTeam,
                CommandDomain.World,
                CommandDescription("TeamLeave_Description", "退出当前队伍"),
                allowedPrincipals: CommandPrincipalKinds.Player,
                supportsRemoteInvocation: true
            )
        );

        commands.Adapters.Register(
            new ResourceId(owner, "server/p2p/start"),
            HttpCommandBinding.Create(_ => new StartP2PCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "server/p2p/stop"),
            HttpCommandBinding.Create(_ => new StopP2PCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "server/p2p/status"),
            HttpCommandBinding.Create(_ => new GetP2PStatusCommand())
        );
        commands.Adapters.Register(new ResourceId(owner, "text/p2p"), CreateP2PText());

        commands.Adapters.Register(new ResourceId(owner, "text/help"), CreateHelpText());
        commands.Adapters.Register(new ResourceId(owner, "text/time"), CreateTimeText());
        commands.Adapters.Register(new ResourceId(owner, "text/tp"), CreateTeleportText());
        commands.Adapters.Register(
            new ResourceId(owner, "text/mark"),
            new TextCommand(
                "mark",
                CommandDescription("MarkPrevious_Description", "标记自己的当前位置（M）"),
                [
                    new CommandRoute(
                        [],
                        typeof(MarkPrivatePositionCommand),
                        _ => new MarkPrivatePositionCommand("previous")
                    ),
                    new CommandRoute(
                        [
                            new CommandLiteral("private"),
                            new CommandArgument("name", SuggestionProvider: SuggestPrivateMarks)
                        ],
                        typeof(MarkPrivatePositionCommand),
                        arguments => new MarkPrivatePositionCommand(arguments.Get<string>("name")),
                        CommandDescription("MarkPrivate_Description", "保存个人标记")
                    ),
                    new CommandRoute(
                        [
                            new CommandLiteral("public"),
                            new CommandArgument("name", SuggestionProvider: SuggestPublicMarks)
                        ],
                        typeof(MarkPublicPositionCommand),
                        arguments => new MarkPublicPositionCommand(arguments.Get<string>("name")),
                        CommandDescription("MarkPublic_Description", "保存公共标记")
                    )
                ]
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "player/teleport/other"),
            HttpCommandBinding.Create(
                arguments => new TeleportPlayerCommand(
                    arguments.Get<string>("player"),
                    ReadTeleportPosition(arguments),
                    arguments.GetOrDefault<string?>("destination", null)
                ),
                new HttpCommandArgumentDefinition("player", "string"),
                new HttpCommandArgumentDefinition("x", "number", false),
                new HttpCommandArgumentDefinition("y", "number", false),
                new HttpCommandArgumentDefinition("z", "number", false),
                new HttpCommandArgumentDefinition("destination", "string", false)
            )
        );
        commands.Adapters.Register(new ResourceId(owner, "text/weather"), CreateWeatherText());
        commands.Adapters.Register(new ResourceId(owner, "text/season"), CreateSeasonText());
        commands.Adapters.Register(new ResourceId(owner, "text/players"), CreatePlayersText());
        commands.Adapters.Register(new ResourceId(owner, "text/stop"), CreateStopText());
        commands.Adapters.Register(new ResourceId(owner, "text/auth"), CreateAuthText());
        commands.Adapters.Register(new ResourceId(owner, "text/permission"), CreatePermissionText());
        commands.Adapters.Register(new ResourceId(owner, "text/team"), CreateTeamText());

        commands.Adapters.Register(
            new ResourceId(owner, "world/time/get"),
            HttpCommandBinding.Create(static _ => new GetWorldTimeCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "world/time/set"),
            HttpCommandBinding.Create(
                static arguments =>
                    new SetWorldTimeCommand(arguments.Get<string>("preset")),
                new HttpCommandArgumentDefinition("preset", "string")
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "world/weather/precipitation/set"),
            HttpCommandBinding.Create(
                arguments => new SetPrecipitationCommand(arguments.Get<bool>("enabled")),
                new HttpCommandArgumentDefinition("enabled", "boolean")
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "world/weather/fog/set"),
            HttpCommandBinding.Create(
                arguments => new SetFogCommand(arguments.Get<bool>("enabled")),
                new HttpCommandArgumentDefinition("enabled", "boolean")
            )
        );
        commands.Adapters.Register(
            new ResourceId(owner, "player/list"),
            HttpCommandBinding.Create(static _ => new ListPlayersCommand())
        );
        commands.Adapters.Register(
            new ResourceId(owner, "server/stop"),
            HttpCommandBinding.Create(static _ => new StopServerCommand())
        );
    }

    private static void RegisterCreativePermission(
        IModCommands commands,
        ResourceId id,
        LocalizedText description
    )
    {
        RegisterPermission(
            commands,
            id,
            CommandDomain.World,
            description: description,
            implicitGrant: WorldControlCommandHandlers.IsCreativePlayer
        );
    }

    private static void RegisterPermission(
        IModCommands commands,
        ResourceId id,
        CommandDomain domain,
        PermissionGrantPolicy grantPolicy = PermissionGrantPolicy.Standard,
        LocalizedText? description = null,
        bool managesStandardPermissions = false,
        Func<CommandPrincipal, Project?, bool>? implicitGrant = null
    )
    {
        commands.Permissions.Register(
            id,
            new CommandPermissionDefinition(
                domain,
                grantPolicy,
                description,
                managesStandardPermissions: managesStandardPermissions,
                implicitGrant: implicitGrant
            )
        );
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
                    CommandDescription("HelpList_Description", "列出当前可用的指令")
                ),
                new CommandRoute(
                    [
                        new CommandArgument(
                            "command",
                            SuggestionProvider: SuggestCommands
                        )
                    ],
                    typeof(ShowCommandHelpCommand),
                    arguments => new ShowCommandHelpCommand(arguments.Get<string>("command")),
                    CommandDescription("HelpDetail_Description", "显示指定指令的用法")
                )
            ],
            ["?"]
        );
    }

    private static Vector3? ReadTeleportPosition(HttpCommandArguments arguments) =>
        arguments.Values.ContainsKey("x") || arguments.Values.ContainsKey("y") || arguments.Values.ContainsKey("z")
            ? new Vector3(arguments.Get<float>("x"), arguments.Get<float>("y"), arguments.Get<float>("z"))
            : null;

    internal static TextCommand CreateTeleportText()
    {
        var self = new CommandLiteral("self", CommandDescription("TeleportSelf_Description", "传送自己"));
        var playerTarget = new CommandLiteral("player", CommandDescription("TeleportOther_Description", "传送指定玩家"));
        CommandSegment[] coordinates =
        [
            new CommandArgument("x", CommandArgumentKind.Number),
            new CommandArgument("y", CommandArgumentKind.Number),
            new CommandArgument("z", CommandArgumentKind.Number)
        ];

        static Vector3 Position(CommandArguments arguments) => new(
            (float)arguments.Get<double>("x"),
            (float)arguments.Get<double>("y"),
            (float)arguments.Get<double>("z")
        );

        return new TextCommand(
            "tp",
            CommandDescription("Teleport_Description", "选择自己或指定玩家，再选择传送目的地"),
            [
                new CommandRoute(
                    [
                        self, new CommandLiteral("player"),
                        new CommandArgument("destination", SuggestionProvider: SuggestTeleportDestinations)
                    ],
                    typeof(TeleportSelfCommand),
                    arguments => new TeleportSelfCommand(DestinationPlayer: arguments.Get<string>("destination")),
                    CommandDescription("TeleportSelfPlayer_Description", "传送自己到玩家")
                ),
                new CommandRoute(
                    [
                        playerTarget, new CommandArgument("player", SuggestionProvider: SuggestCharacters),
                        new CommandLiteral("position"), .. coordinates
                    ],
                    typeof(TeleportPlayerCommand),
                    arguments => new TeleportPlayerCommand(arguments.Get<string>("player"), Position(arguments)),
                    CommandDescription("TeleportOtherPosition_Description", "传送指定玩家到坐标")
                ),
                new CommandRoute(
                    [
                        playerTarget, new CommandArgument("player", SuggestionProvider: SuggestCharacters),
                        new CommandLiteral("player"),
                        new CommandArgument("destination", SuggestionProvider: SuggestTeleportDestinations)
                    ],
                    typeof(TeleportPlayerCommand),
                    arguments => new TeleportPlayerCommand(
                        arguments.Get<string>("player"),
                        DestinationPlayer: arguments.Get<string>("destination")
                    ),
                    CommandDescription("TeleportOtherPlayer_Description", "传送指定玩家到玩家")
                ),
                new CommandRoute(
                    [
                        playerTarget, new CommandArgument("player", SuggestionProvider: SuggestCharacters),
                        new CommandLiteral("private"),
                        new CommandArgument("name", SuggestionProvider: SuggestTargetPrivateMarks)
                    ],
                    typeof(TeleportPlayerMarkCommand),
                    arguments =>
                        new TeleportPlayerMarkCommand(
                            arguments.Get<string>("player"),
                            arguments.Get<string>("name"),
                            false
                        ),
                    CommandDescription("TeleportOtherMark_Description", "传送指定玩家到标记")
                ),
                new CommandRoute(
                    [
                        playerTarget, new CommandArgument("player", SuggestionProvider: SuggestCharacters),
                        new CommandLiteral("public"),
                        new CommandArgument("name", SuggestionProvider: SuggestPublicMarks)
                    ],
                    typeof(TeleportPlayerMarkCommand),
                    arguments =>
                        new TeleportPlayerMarkCommand(
                            arguments.Get<string>("player"),
                            arguments.Get<string>("name"),
                            true
                        ),
                    CommandDescription("TeleportOtherMark_Description", "传送指定玩家到标记")
                ),
                new CommandRoute(
                    [
                        self, new CommandLiteral("private"),
                        new CommandArgument("name", SuggestionProvider: SuggestPrivateMarks)
                    ],
                    typeof(TeleportMarkCommand),
                    arguments => new TeleportMarkCommand(arguments.Get<string>("name"), false),
                    CommandDescription("TeleportMark_Description", "传送到命名标记")
                ),
                new CommandRoute(
                    [
                        self, new CommandLiteral("public"),
                        new CommandArgument("name", SuggestionProvider: SuggestPublicMarks)
                    ],
                    typeof(TeleportMarkCommand),
                    arguments => new TeleportMarkCommand(arguments.Get<string>("name"), true),
                    CommandDescription("TeleportMark_Description", "传送到命名标记")
                ),
                new CommandRoute(
                    [],
                    typeof(TeleportMarkCommand),
                    _ => new TeleportMarkCommand("previous", false),
                    CommandDescription("TeleportPrevious_Description", "传送到自己的返回点")
                ),
                new CommandRoute(
                    [self, new CommandLiteral("position"), .. coordinates],
                    typeof(TeleportSelfCommand),
                    arguments => new TeleportSelfCommand(Position(arguments)),
                    CommandDescription("TeleportSelfPosition_Description", "传送自己到坐标")
                )
            ]
        );
    }

    internal static TextCommand CreateP2PText()
    {
        var routes = new List<CommandRoute>
        {
            new(
                [new CommandLiteral("status")],
                typeof(GetP2PStatusCommand),
                _ => new GetP2PStatusCommand(),
                CommandDescription("P2PStatus_Description", "查询 P2P 房间状态")
            ),
            new(
                [new CommandLiteral("stop")],
                typeof(StopP2PCommand),
                _ => new StopP2PCommand(),
                CommandDescription("P2PStop_Description", "停止 P2P 房间")
            ),
            new(
                [new CommandLiteral("start")],
                typeof(StartP2PCommand),
                _ => new StartP2PCommand(),
                CommandDescription("P2PStart_Description", "开启 P2P 房间")
            )
        };
        return new TextCommand("p2p", CommandDescription("P2P_Description", "管理 P2P 房间"), routes);
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
                    CommandDescription("TimeGet_Description", "显示当前世界时间")
                ),
                new CommandRoute(
                    [
                        new CommandLiteral("set"),
                        new CommandArgument(
                            "value",
                            CommandArgumentKind.String,
                            choices,
                            _ => TimeSuggestions()
                        )
                    ],
                    typeof(SetWorldTimeCommand),
                    arguments => new SetWorldTimeCommand(arguments.Get<string>("value")),
                    CommandDescription("TimeSet_Description", "设置世界时间")
                )
            ]
        );
    }

    internal static TextCommand CreatePlayersText()
    {
        return new TextCommand(
            "players",
            CommandDescription("Players_Description", "列出已知角色及其在线状态"),
            [
                new CommandRoute(
                    [],
                    typeof(ListPlayersCommand),
                    _ => new ListPlayersCommand(),
                    CommandDescription("PlayersList_Description", "显示角色列表")
                )
            ],
            ["list"]
        );
    }

    internal static TextCommand CreateStopText()
    {
        return new TextCommand(
            "stop",
            CommandDescription("Stop_Description", "保存世界并停止服务器"),
            [
                new CommandRoute(
                    [],
                    typeof(StopServerCommand),
                    _ => new StopServerCommand(),
                    CommandDescription("StopAction_Description", "停止服务器")
                )
            ]
        );
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
                                "停止降水"
                            )
                        )
                    ],
                    typeof(SetPrecipitationCommand),
                    arguments => new SetPrecipitationCommand(ParseToggle(arguments.Get<string>("enabled"))),
                    CommandDescription("WeatherRain_Description", "控制降水")
                ),
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
                                "关闭雾气"
                            )
                        )
                    ],
                    typeof(SetFogCommand),
                    arguments => new SetFogCommand(ParseToggle(arguments.Get<string>("enabled"))),
                    CommandDescription("WeatherFog_Description", "控制雾气")
                )
            ]
        );
    }

    internal static TextCommand CreateSeasonText()
    {
        var seasons = new[] { "summer", "autumn", "winter", "spring" };
        var progressStages = new[] { "start", "middle", "end" };
        return new TextCommand(
            "season",
            CommandDescription("Season_Description", "调整当前季节，环境将逐步更新"),
            [
                new CommandRoute(
                    [
                        new CommandLiteral("set"),
                        new CommandArgument(
                            "season",
                            CommandArgumentKind.String,
                            seasons,
                            _ => SeasonSuggestions()
                        )
                    ],
                    typeof(SetSeasonCommand),
                    arguments => new SetSeasonCommand(
                        Enum.Parse<Season>(
                            arguments.Get<string>("season"),
                            ignoreCase: true
                        ),
                        0f
                    ),
                    CommandDescription("SeasonSet_Description", "调整季节，环境将逐步更新")
                ),
                new CommandRoute(
                    [
                        new CommandLiteral("set"),
                        new CommandArgument(
                            "season",
                            CommandArgumentKind.String,
                            seasons,
                            _ => SeasonSuggestions()
                        ),
                        new CommandArgument(
                            "progress",
                            CommandArgumentKind.String,
                            progressStages,
                            _ => SeasonProgressSuggestions()
                        )
                    ],
                    typeof(SetSeasonCommand),
                    arguments => new SetSeasonCommand(
                        Enum.Parse<Season>(
                            arguments.Get<string>("season"),
                            ignoreCase: true
                        ),
                        ParseSeasonProgress(arguments.Get<string>("progress"))
                    ),
                    CommandDescription("SeasonProgress_Description", "调整季节及进度，环境将逐步更新")
                )
            ]
        );
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
                    CommandDescription("AuthHelp_Description", "显示服务器认领帮助")
                ),
                new CommandRoute(
                    [
                        new CommandLiteral("claim"),
                        new CommandArgument("code")
                    ],
                    typeof(ClaimServerAdministrationCommand),
                    arguments => new ClaimServerAdministrationCommand(arguments.Get<string>("code")),
                    CommandDescription(
                        "AuthClaim_Description",
                        "使用认领码初始化在线玩家的管理权限"
                    )
                ),
                new CommandRoute(
                    [new CommandLiteral("status")],
                    typeof(GetServerAuthStatusCommand),
                    _ => new GetServerAuthStatusCommand(),
                    CommandDescription("AuthStatus_Description", "查看服务器认领状态")
                ),
                new CommandRoute(
                    [new CommandLiteral("code")],
                    typeof(GetServerAuthCodeCommand),
                    _ => new GetServerAuthCodeCommand(),
                    CommandDescription("AuthCode_Description", "显示当前服务器认领码")
                ),
                new CommandRoute(
                    [new CommandLiteral("regenerate")],
                    typeof(RegenerateServerAuthCodeCommand),
                    _ => new RegenerateServerAuthCodeCommand(),
                    CommandDescription(
                        "AuthRegenerate_Description",
                        "重新生成服务器认领码"
                    )
                )
            ]
        );
    }

    internal static TextCommand CreatePermissionText()
    {
        return new TextCommand(
            "permission",
            CommandDescription("Permission_Description", "查看和管理主体指令权限"),
            [
                new CommandRoute(
                    [],
                    typeof(ShowPermissionHelpCommand),
                    _ => new ShowPermissionHelpCommand(),
                    CommandDescription("PermissionHelp_Description", "显示授权指令帮助")
                ),
                new CommandRoute(
                    [new CommandLiteral("players")],
                    typeof(ListPermissionPlayersCommand),
                    _ => new ListPermissionPlayersCommand(),
                    CommandDescription(
                        "PermissionPlayers_Description",
                        "列出当前可授权主体"
                    )
                ),
                new CommandRoute(
                    [new CommandLiteral("nodes")],
                    typeof(ListPermissionNodesCommand),
                    _ => new ListPermissionNodesCommand(),
                    CommandDescription(
                        "PermissionNodes_Description",
                        "列出当前可授权权限节点"
                    )
                ),
                new CommandRoute(
                    [new CommandLiteral("list")],
                    typeof(ListOwnPermissionsCommand),
                    _ => new ListOwnPermissionsCommand(),
                    CommandDescription(
                        "PermissionListSelf_Description",
                        "查看自己的指令权限"
                    )
                ),
                new CommandRoute(
                    [
                        new CommandLiteral("list"),
                        new CommandArgument("player", SuggestionProvider: SuggestPlayers)
                    ],
                    typeof(ListPlayerPermissionsCommand),
                    arguments => new ListPlayerPermissionsCommand(arguments.Get<string>("player")),
                    CommandDescription(
                        "PermissionListPlayer_Description",
                        "查看指定主体的指令权限"
                    )
                ),
                new CommandRoute(
                    [
                        new CommandLiteral("grant"),
                        new CommandArgument("player", SuggestionProvider: SuggestPlayers),
                        PermissionArgument(SuggestGrantablePermissionNodes)
                    ],
                    typeof(GrantPlayerPermissionCommand),
                    arguments => new GrantPlayerPermissionCommand(
                        arguments.Get<string>("player"),
                        arguments.Get<string>("permission"),
                        false
                    ),
                    CommandDescription(
                        "PermissionGrant_Description",
                        "授予主体使用权限"
                    )
                ),
                new CommandRoute(
                    [
                        new CommandLiteral("delegate"),
                        new CommandArgument("player", SuggestionProvider: SuggestPlayers),
                        PermissionArgument(SuggestDelegablePermissionNodes)
                    ],
                    typeof(GrantPlayerPermissionCommand),
                    arguments => new GrantPlayerPermissionCommand(
                        arguments.Get<string>("player"),
                        arguments.Get<string>("permission"),
                        true
                    ),
                    CommandDescription(
                        "PermissionDelegate_Description",
                        "授予用户使用及再授权权限"
                    )
                ),
                new CommandRoute(
                    [
                        new CommandLiteral("revoke"),
                        new CommandArgument("player", SuggestionProvider: SuggestPlayers),
                        PermissionArgument(SuggestRevocablePermissionNodes)
                    ],
                    typeof(RevokePlayerPermissionCommand),
                    arguments => new RevokePlayerPermissionCommand(
                        arguments.Get<string>("player"),
                        arguments.Get<string>("permission")
                    ),
                    CommandDescription(
                        "PermissionRevoke_Description",
                        "撤销主体的指定权限"
                    )
                )
            ],
            ["perm"]
        );
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
                    arguments => new CreateTeamCommand(arguments.Get<string>("name")),
                    CommandDescription("TeamCreate_Description", "创建队伍")
                ),
                new CommandRoute(
                    [
                        new CommandLiteral("join"),
                        new CommandArgument(
                            "team",
                            CommandArgumentKind.Guid
                        )
                    ],
                    typeof(RequestJoinTeamCommand),
                    arguments => new RequestJoinTeamCommand(arguments.Get<Guid>("team")),
                    CommandDescription("TeamJoin_Description", "申请加入指定队伍")
                ),
                new CommandRoute(
                    [new CommandLiteral("leave")],
                    typeof(LeaveTeamCommand),
                    _ => new LeaveTeamCommand(),
                    CommandDescription("TeamLeave_Description", "退出当前队伍")
                )
            ]
        );
    }

    private static CommandArgument PlayerArgument()
    {
        return new CommandArgument(
            "player",
            SuggestionProvider: SuggestCharacters
        );
    }

    private static CommandArgument PermissionArgument(
        Func<CommandSuggestionContext, IEnumerable<CommandArgumentSuggestion>> provider
    )
    {
        return new CommandArgument(
            "permission",
            SuggestionProvider: provider
        );
    }

    private static CommandResult ExecuteHelp(
        CommandContext context,
        ShowCommandHelpCommand command
    )
    {
        if (!string.IsNullOrWhiteSpace(command.CommandName))
        {
            return ExecuteHelpForCommand(context, command.CommandName);
        }

        var textAdapter = new TextCommandAdapter(context.Registry, context.Project);
        var names = textAdapter.Entries
            .Where(entry =>
                entry.Command.Routes.Any(route =>
                    context.Registry.CanInvoke(
                        route.CommandType,
                        context.Principal,
                        context.Project
                    )
                )
            )
            .Select(entry => "/" + entry.Command.Name)
            .ToArray();
        return names.Length == 0
            ? CommandResult.LocalizedHistoryOk(
                "command.help",
                "HelpEmpty_Message",
                "当前没有可用指令。"
            )
            : CommandResult.LocalizedHistoryOk(
                "command.help",
                "HelpHeading_Message",
                "可用指令：\n{0}",
                string.Join("\n", names.Select(item => $"- {item}"))
            );
    }

    private static CommandResult ExecuteHelpForCommand(
        CommandContext context,
        string commandName
    )
    {
        var name = commandName.TrimStart('/');
        var textAdapter = new TextCommandAdapter(context.Registry, context.Project);
        if (!textAdapter.TryFind(name, out var registered) || registered is null)
        {
            return CommandResult.LocalizedFail(
                "command.unknown",
                "HelpUnknown_Message",
                "未知指令：{0}。",
                name
            );
        }

        var routes = registered.Command.Routes
            .Where(route =>
                context.Registry.CanInvoke(
                    route.CommandType,
                    context.Principal,
                    context.Project
                )
            )
            .Select(route =>
                {
                    var suffix = string.Join(
                        " ",
                        route.Segments.Select(segment => segment switch
                            {
                                CommandLiteral literal => literal.Value,
                                CommandArgument argument => $"<{argument.Name}>",
                                _ => string.Empty
                            }
                        )
                    );
                    return string.IsNullOrEmpty(suffix)
                        ? $"/{registered.Command.Name}"
                        : $"/{registered.Command.Name} {suffix}";
                }
            )
            .ToArray();
        return routes.Length == 0
            ? CommandResult.LocalizedFail(
                "command.forbidden",
                "HelpForbidden_Message",
                "你没有查看该指令的权限。"
            )
            : new CommandResult(
                true,
                "command.help.detail",
                LocalizedText.Format(
                    CommandDescription("HelpDetail_Message", "{0}\n用法：\n{1}"),
                    registered.Command.Description,
                    LocalizedText.Literal(string.Join("\n", routes.Select(item => $"- {item}")))
                ),
                Presentation: CommandResultPresentation.History
            );
    }

    private static CommandResult ExecuteTimeGet(
        CommandContext context,
        GetWorldTimeCommand command
    )
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
            time.Day.ToString("0.00")
        );
    }

    private static CommandResult ExecutePlayerList(
        CommandContext context,
        ListPlayersCommand command
    )
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        var players = GetCharacters(context.Project)
            .Select(player => new
                {
                    player.Name,
                    Online = player.ControllerConnection is not null || player.IsLocalCharacter
                }
            )
            .OrderByDescending(player => player.Online)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (players.Length == 0)
        {
            return CommandResult.LocalizedOk(
                "player.list",
                "PlayerListEmpty_Message",
                "角色列表为空。"
            );
        }

        var onlineCount = players.Count(player => player.Online);
        var entries = players.Select(player =>
            $"{(player.Online ? "●" : "○")} {player.Name}"
        );
        return CommandResult.LocalizedOk(
            "player.list",
            "PlayerListHeading_Message",
            "角色 {0} 个（在线 {1}）：\n{2}",
            players.Length.ToString(),
            onlineCount.ToString(),
            string.Join("\n", entries.Select(item => $"- {item}"))
        );
    }

    private static float ParseSeasonProgress(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "start" => 0f,
            "middle" => 0.5f,
            "end" => 0.999f,
            _ => throw new InvalidOperationException($"Unsupported season progress {value}.")
        };
    }

    private static bool ParseToggle(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "enable" => true,
            "disable" => false,
            _ => throw new InvalidOperationException($"Unsupported toggle value {value}.")
        };
    }

    private static IEnumerable<CommandArgumentSuggestion> ToggleSuggestions(
        string enableKey,
        string enableFallback,
        string disableKey,
        string disableFallback
    )
    {
        return
        [
            new CommandArgumentSuggestion(
                "enable",
                CommandDescription(enableKey, enableFallback)
            ),
            new CommandArgumentSuggestion(
                "disable",
                CommandDescription(disableKey, disableFallback)
            )
        ];
    }

    private static IEnumerable<CommandArgumentSuggestion> SeasonSuggestions()
    {
        return
        [
            new CommandArgumentSuggestion(
                "summer",
                CommandDescription("SeasonSummer_Description", "夏季")
            ),
            new CommandArgumentSuggestion(
                "autumn",
                CommandDescription("SeasonAutumn_Description", "秋季")
            ),
            new CommandArgumentSuggestion(
                "winter",
                CommandDescription("SeasonWinter_Description", "冬季")
            ),
            new CommandArgumentSuggestion(
                "spring",
                CommandDescription("SeasonSpring_Description", "春季")
            )
        ];
    }

    private static IEnumerable<CommandArgumentSuggestion> TimeSuggestions()
    {
        return
        [
            new CommandArgumentSuggestion(
                "sunrise",
                new LocalizedText("TimeOfDayMode", "Sunrise", "黎明")
            ),
            new CommandArgumentSuggestion(
                "day",
                new LocalizedText("TimeOfDayMode", "Day", "中午")
            ),
            new CommandArgumentSuggestion(
                "sunset",
                new LocalizedText("TimeOfDayMode", "Sunset", "黄昏")
            ),
            new CommandArgumentSuggestion(
                "night",
                new LocalizedText("TimeOfDayMode", "Night", "午夜")
            )
        ];
    }

    private static IEnumerable<CommandArgumentSuggestion> SeasonProgressSuggestions()
    {
        return
        [
            new CommandArgumentSuggestion(
                "start",
                CommandDescription("SeasonStart_Description", "季节初期")
            ),
            new CommandArgumentSuggestion(
                "middle",
                CommandDescription("SeasonMiddle_Description", "季节中期")
            ),
            new CommandArgumentSuggestion(
                "end",
                CommandDescription("SeasonEnd_Description", "季节末期")
            )
        ];
    }

    private static LocalizedText CommandDescription(
        string key,
        string fallback
    )
    {
        return new LocalizedText("Commands", key, fallback);
    }

    private static CommandResult ExecutePermissionListSelf(
        CommandContext context,
        ListOwnPermissionsCommand command
    )
    {
        if (context.Principal.Subject is null)
        {
            return CommandResult.LocalizedFail(
                "permission.player_required",
                "PermissionConsolePlayerRequired_Message",
                "服务器控制台请指定主体地址：permission list <player>。"
            );
        }

        return FormatPermissionList(context.Principal.Subject);
    }

    private static CommandResult ExecuteAuthHelp(
        CommandContext context,
        ShowServerAuthHelpCommand command
    )
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        return ServerAdministrationBootstrap.IsClaimed(context.Project)
            ? CommandResult.LocalizedHistoryOk(
                "auth.claimed",
                "AuthClaimed_Message",
                "服务器管理员已经完成首次认领。"
            )
            : context.Principal.Is(CommandPrincipalKind.LocalOperator)
                ? CommandResult.LocalizedHistoryOk(
                    "auth.unclaimed",
                    "AuthUnclaimedConsole_Message",
                    "服务器尚未认领。\n使用 auth code 查看认领码，玩家上线后执行 /auth claim <认领码>。"
                )
                : CommandResult.LocalizedHistoryOk(
                    "auth.unclaimed",
                    "AuthUnclaimedPlayer_Message",
                    "服务器尚未认领。\n请输入 /auth claim <认领码> 完成首次管理员授权。"
                );
    }

    private static CommandResult ExecuteAuthClaim(
        CommandContext context,
        ClaimServerAdministrationCommand command
    )
    {
        if (!EnsureServer(context.Project, out var failure))
        {
            return failure;
        }

        if (context.Project is null)
        {
            return NoWorld();
        }

        if (context.Principal.Subject is not { } player)
        {
            return CommandResult.LocalizedFail(
                "auth.player_required",
                "AuthPlayerRequired_Message",
                "认领必须由已连接服务器的在线玩家执行。"
            );
        }

        var result = ServerAdministrationBootstrap.TryClaim(
            context.Project,
            player,
            command.Code
        );
        if (!result.Success)
        {
            return new CommandResult(
                false,
                result.Code,
                result.Message
            );
        }

        SynchronizePermissions(context.Project!, player);
        WorldProjectPersistence.For(context.Project!).Save(false);
        return new CommandResult(
            true,
            result.Code,
            result.Message
        );
    }

    private static CommandResult ExecuteAuthStatus(
        CommandContext context,
        GetServerAuthStatusCommand command
    )
    {
        if (context.Project is null)
        {
            return NoWorld();
        }

        return ServerAdministrationBootstrap.IsClaimed(context.Project)
            ? CommandResult.LocalizedOk(
                "auth.claimed",
                "AuthClaimed_Message",
                "服务器管理员已经完成首次认领。"
            )
            : CommandResult.LocalizedOk(
                "auth.unclaimed",
                "AuthUnclaimedStatus_Message",
                "服务器尚未认领，必须由在线玩家提交认领码。"
            );
    }

    private static CommandResult ExecuteAuthCode(
        CommandContext context,
        GetServerAuthCodeCommand command
    )
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
                code
            )
            : CommandResult.LocalizedFail(
                "auth.already_claimed",
                "AuthClaimed_Message",
                "服务器管理员已经完成首次认领。"
            );
    }

    private static CommandResult ExecuteAuthRegenerate(
        CommandContext context,
        RegenerateServerAuthCodeCommand command
    )
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
                code
            )
            : CommandResult.LocalizedFail(
                "auth.already_claimed",
                "AuthClaimed_Message",
                "服务器管理员已经完成首次认领。"
            );
    }

    private static CommandResult ExecutePermissionHelp(
        CommandContext context,
        ShowPermissionHelpCommand command
    )
    {
        var players = GetPlayers(context.Project).Select(player => player.Address.Value).ToArray();
        var nodes = context.Registry.Permissions.Definitions
            .Select(node => node.Id)
            .Where(node => context.Registry.Permissions.CanGrant(
                    node,
                    context.Principal,
                    context.Project
                )
            )
            .Select(node => node.ToString())
            .ToArray();
        return CommandResult.LocalizedHistoryOk(
            "permission.help",
            "PermissionHelp_Message",
            "授权用法：\n- /permission players（列出存档玩家）\n- /permission grant <player> <permission>（仅使用）\n- /permission delegate <player> <permission>（允许再授权）\n- /permission revoke <player> <permission>\n- /permission list [player]\nplayer 为玩家身份地址，支持离线及尚无角色的玩家。\n当前玩家：\n{0}\n可授权节点：\n{1}",
            FormatItems(players),
            FormatItems(nodes)
        );
    }

    private static CommandResult ExecutePermissionPlayers(
        CommandContext context,
        ListPermissionPlayersCommand command
    )
    {
        var players = GetPlayers(context.Project).ToArray();
        return players.Length == 0
            ? CommandResult.LocalizedOk(
                "permission.players",
                "PermissionPlayersEmpty_Message",
                "当前没有可授权的主体。"
            )
            : CommandResult.LocalizedOk(
                "permission.players",
                "PermissionPlayersHeading_Message",
                "当前主体：\n{0}",
                FormatItems(
                    players.Select(player =>
                        $"{player.Address} ({player.DisplayName})"
                    )
                )
            );
    }

    private static CommandResult ExecutePermissionNodes(
        CommandContext context,
        ListPermissionNodesCommand command
    )
    {
        var nodes = context.Registry.Permissions.Definitions
            .Select(node => node.Id)
            .Where(node => context.Registry.Permissions.CanGrant(
                    node,
                    context.Principal,
                    context.Project
                )
            )
            .Select(node => node.ToString())
            .ToArray();
        return nodes.Length == 0
            ? CommandResult.LocalizedOk(
                "permission.nodes",
                "PermissionNodesEmpty_Message",
                "当前没有可再授权的权限节点。"
            )
            : CommandResult.LocalizedOk(
                "permission.nodes",
                "PermissionNodesHeading_Message",
                "可授权节点：\n{0}",
                FormatItems(nodes)
            );
    }

    private static CommandResult ExecutePermissionListPlayer(
        CommandContext context,
        ListPlayerPermissionsCommand command
    )
    {
        return TryFindPlayer(context, command.Player, out var player, out var failure)
            ? FormatPermissionList(player!)
            : failure!;
    }

    private static CommandResult ExecutePermissionGrant(
        CommandContext context,
        GrantPlayerPermissionCommand command
    )
    {
        if (!EnsureServer(context.Project, out var failure))
        {
            return failure;
        }

        if (!TryParsePermission(command.Permission, out var permission))
        {
            return CommandResult.LocalizedFail(
                "permission.invalid",
                "PermissionInvalid_Message",
                "权限节点格式无效。"
            );
        }

        if (!context.Registry.Permissions.CanGrant(
                permission,
                context.Principal,
                context.Project,
                command.CanDelegate
            ))
        {
            return CommandResult.LocalizedFail(
                "permission.cannot_delegate",
                "PermissionCannotGrant_Message",
                "权限节点 {0} 不可授权，或你没有对应的管理范围。",
                permission.ToString()
            );
        }

        if (!TryFindPlayer(context, command.Player, out var player, out failure))
        {
            return failure!;
        }

        var changed = player!.Permissions.Grant(permission, command.CanDelegate);
        if (changed)
        {
            SynchronizePermissions(context.Project!, player);
        }

        if (!changed)
        {
            return CommandResult.LocalizedOk(
                "permission.unchanged",
                "PermissionUnchanged_Message",
                "{0} 已拥有相同或更高范围的 {1} 权限。",
                player.Address.Value,
                permission.ToString()
            );
        }

        return CommandResult.LocalizedOk(
            "permission.granted",
            command.CanDelegate
                ? "PermissionGrantedDelegate_Message"
                : "PermissionGrantedUse_Message",
            command.CanDelegate
                ? "已授予 {0} 权限 {1}（允许再授权）。"
                : "已授予 {0} 权限 {1}（仅允许使用）。",
            player.Address.Value,
            permission.ToString()
        );
    }

    private static CommandResult ExecutePermissionRevoke(
        CommandContext context,
        RevokePlayerPermissionCommand command
    )
    {
        if (!EnsureServer(context.Project, out var failure))
        {
            return failure;
        }

        if (!TryParsePermission(command.Permission, out var permission))
        {
            return CommandResult.LocalizedFail(
                "permission.invalid",
                "PermissionInvalid_Message",
                "权限节点格式无效。"
            );
        }

        if (!context.Registry.Permissions.CanGrant(
                permission,
                context.Principal,
                context.Project
            ))
        {
            return CommandResult.LocalizedFail(
                "permission.cannot_delegate",
                "PermissionCannotRevoke_Message",
                "权限节点 {0} 不可撤销，或你没有对应的管理范围。",
                permission.ToString()
            );
        }

        if (!TryFindPlayer(context, command.Player, out var player, out failure))
        {
            return failure!;
        }

        if (!player!.Permissions.Revoke(permission))
        {
            return CommandResult.LocalizedFail(
                "permission.not_found",
                "PermissionNotHeld_Message",
                "{0} 没有直接持有权限 {1}。",
                player.Address.Value,
                permission.ToString()
            );
        }

        SynchronizePermissions(context.Project!, player);
        return CommandResult.LocalizedOk(
            "permission.revoked",
            "PermissionRevoked_Message",
            "已撤销 {0} 的权限 {1}。",
            player.Address.Value,
            permission.ToString()
        );
    }

    private static CommandResult ExecuteTimeSet(
        CommandContext context,
        SetWorldTimeCommand command
    )
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
        NetworkSender.Send(
            context.Project,
            new SubsystemTimePackage(
                time.SubsystemGameInfo.TotalElapsedGameTime,
                time.TimeOfDayOffset
            ),
            PackageAudience.Global
        );

        return WorldToast(
            "world.time.changed",
            "TimeChanged_Message",
            "已将世界时间设置为 {0}。",
            ResolveTimePresetName(value)
        );
    }

    private static CommandResult ExecuteTimeAdvance(
        CommandContext context,
        AdvanceWorldTimeCommand command
    )
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
                Offset: IntervalUtils.Interval(time.TimeOfDay, target.Item2))
            )
            .MinBy(target => target.Offset);
        time.TimeOfDayOffset += next.Offset;
        NetworkSender.Send(
            context.Project,
            new SubsystemTimePackage(
                time.SubsystemGameInfo.TotalElapsedGameTime,
                time.TimeOfDayOffset
            ),
            PackageAudience.Global
        );

        return WorldToast(
            "world.time.advanced",
            "TimeAdvanced_Message",
            "已将世界时间推进到 {0}。",
            ResolveTimePresetName(next.Name)
        );
    }

    private static CommandResult WorldToast(string code, string key, string fallback, LocalizedText argument) => new(
        true,
        code,
        LocalizedText.Format(CommandDescription(key, fallback), argument),
        Audience: CommandResultAudience.AllPlayers,
        Presentation: CommandResultPresentation.Toast
    );

    private static LocalizedText ResolveTimePresetName(string value)
    {
        var (key, fallback) = value.ToLowerInvariant() switch
        {
            "sunrise" => ("Sunrise", "黎明"),
            "day" => ("Day", "中午"),
            "sunset" => ("Sunset", "黄昏"),
            "night" => ("Night", "午夜"),
            _ => throw new InvalidOperationException($"Unsupported time value {value}.")
        };
        return new LocalizedText("TimeOfDayMode", key, fallback);
    }

    private static CommandResult ExecuteStop(
        CommandContext context,
        StopServerCommand command
    )
    {
        if (context.Project is null ||
            !WorldRuntimeContext.For(context.Project).IsAuthority)
        {
            return CommandResult.LocalizedFail(
                "server.no_authority",
                "NoAuthorityWorld_Message",
                "当前没有权威世界。"
            );
        }

        context.Project!.FindSubsystem<SubsystemWorldLifecycle>(true)!.RequestStop();
        return CommandResult.LocalizedOk(
            "server.stopping",
            "ServerStopping_Message",
            "服务端正在保存并停止。"
        );
    }

    private static CommandResult NoWorld()
    {
        return CommandResult.LocalizedFail(
            "command.no_world",
            "NoWorld_Message",
            "当前没有加载世界。"
        );
    }

    private static bool EnsureServer(Project? project, out CommandResult failure)
    {
        if (project is { IsDisposed: false } &&
            WorldRuntimeContext.For(project) is { IsAuthority: true, IsClosed: false })
        {
            failure = null!;
            return true;
        }

        failure = CommandResult.LocalizedFail(
            "permission.server_only",
            "ServerOnly_Message",
            "该操作只能在服务器上执行。"
        );
        return false;
    }

    private static bool TryFindPlayer(
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

        player = FindPlayer(context.Project, value);
        if (player is not null)
        {
            failure = null;
            return true;
        }

        failure = CommandResult.LocalizedFail(
            "permission.player_not_found",
            "PlayerIdentityNotFound_Message",
            "找不到主体地址：{0}。",
            value
        );
        return false;
    }

    private static WorldPlayer? FindPlayer(Project? project, string value)
    {
        if (project is null) { return null; }

        try
        {
            return project.FindSubsystem<SubsystemPlayers>(true)!.Store.Find(new PlayerAddress(value));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static IEnumerable<WorldPlayer> GetPlayers(Project? project) =>
        project?.FindSubsystem<SubsystemPlayers>(true)!.Store.Entries ?? [];

    private static IEnumerable<CommandArgumentSuggestion> SuggestPlayers(CommandSuggestionContext context)
    {
        return GetPlayers(context.Project).Select(player => new CommandArgumentSuggestion(
                player.Address.Value,
                LocalizedText.Literal(player.DisplayName)
            )
        );
    }

    private static CharacterData? FindCharacter(Project? project, string value)
    {
        if (project is null)
        {
            return null;
        }

        var players = project.FindSubsystem<SubsystemCharacters>(true)!;
        var isGuid = Guid.TryParse(value, out var guid);
        return players.FindCharacterData(candidate =>
            isGuid
                ? candidate.CharacterId == guid
                : string.Equals(candidate.Name, value, StringComparison.OrdinalIgnoreCase)
        );
    }

    private static bool TryParsePermission(
        string value,
        out ResourceId permission
    )
    {
        var separator = value.IndexOf(':');
        if (separator <= 0 ||
            separator == value.Length - 1 ||
            value.IndexOf(':', separator + 1) >= 0)
        {
            permission = default;
            return false;
        }

        try
        {
            permission = new ResourceId(
                new ModId(value[..separator]),
                value[(separator + 1)..]
            );
            return true;
        }
        catch (ArgumentException)
        {
            permission = default;
            return false;
        }
    }

    private static IEnumerable<CharacterData> GetCharacters(Project? project)
    {
        return project?.FindSubsystem<SubsystemCharacters>(true)?.Characters ?? [];
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestPrivateMarks(CommandSuggestionContext context)
    {
        var names = context.Principal.Subject is { } subject
            ? context.Project?.FindSubsystem<SubsystemPositionMarks>(true)!.GetPrivateMarks(subject.Address).Names ?? []
            : [];
        return names.Select(name => new CommandArgumentSuggestion(name));
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestPublicMarks(CommandSuggestionContext context) =>
        (context.Project?.FindSubsystem<SubsystemPositionMarks>()?.PublicMarks.Names ?? []).Select(name =>
            new CommandArgumentSuggestion(name)
        );

    private static IEnumerable<CommandArgumentSuggestion> SuggestTargetPrivateMarks(CommandSuggestionContext context)
    {
        if (context.CompletedTokens.Count < 2 ||
            context.Project is not { } project ||
            FindCharacter(project, context.CompletedTokens[1]) is not { } target)
        {
            return [];
        }

        var players = project.FindSubsystem<SubsystemCharacters>(true)!;
        return WorldConnectionQuery.GetController(project, target.CharacterId) is { } subject
            ? context.Project!.FindSubsystem<SubsystemPositionMarks>(true)!.GetPrivateMarks(subject).Names
                .Select(name => new CommandArgumentSuggestion(name))
            : [];
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestTeleportDestinations(CommandSuggestionContext context)
    {
        var players = GetCharacters(context.Project).ToArray();
        var target = context.CompletedTokens.Count >= 2 &&
                     string.Equals(context.CompletedTokens[0], "player", StringComparison.OrdinalIgnoreCase)
            ? players.FirstOrDefault(player =>
                TeleportCommandHandlers.MatchesPlayer(player, context.CompletedTokens[1])
            )
            : context.Principal.Character;
        return players.Where(player => !ReferenceEquals(player, target)).Select(player =>
            new CommandArgumentSuggestion(player.Name, LocalizedText.Literal(player.CharacterId.ToString()))
        );
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestCharacters(CommandSuggestionContext context)
    {
        return GetCharacters(context.Project).Select(player =>
            new CommandArgumentSuggestion(
                player.Name,
                LocalizedText.Literal(player.CharacterId.ToString())
            )
        );
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestCommands(CommandSuggestionContext context)
    {
        var textAdapter = new TextCommandAdapter(context.Registry, context.Project);
        return textAdapter.Entries
            .Where(entry =>
                entry.Command.Routes.Any(route =>
                    context.Registry.CanInvoke(
                        route.CommandType,
                        context.Principal,
                        context.Project
                    )
                )
            )
            .Select(entry => new CommandArgumentSuggestion(
                    entry.Command.Name,
                    entry.Command.Description
                )
            );
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestGrantablePermissionNodes(
        CommandSuggestionContext context
    ) =>
        SuggestPermissionNodes(context, false);

    private static IEnumerable<CommandArgumentSuggestion> SuggestDelegablePermissionNodes(
        CommandSuggestionContext context
    ) =>
        SuggestPermissionNodes(context, true);

    private static IEnumerable<CommandArgumentSuggestion> SuggestPermissionNodes(
        CommandSuggestionContext context,
        bool canDelegate
    )
    {
        return context.Registry.Permissions.Definitions
            .Where(node => context.Registry.Permissions.CanGrant(
                    node.Id,
                    context.Principal,
                    context.Project,
                    canDelegate
                )
            )
            .Select(node => new CommandArgumentSuggestion(
                    node.Id.ToString(),
                    node.Definition.Description
                )
            );
    }

    private static IEnumerable<CommandArgumentSuggestion> SuggestRevocablePermissionNodes(
        CommandSuggestionContext context
    )
    {
        if (context.CompletedTokens.Count < 2 ||
            FindPlayer(context.Project, context.CompletedTokens[1]) is not { } player)
        {
            return [];
        }

        return player.Permissions.Grants
            .Where(grant => context.Registry.Permissions.CanGrant(
                    grant.Permission,
                    context.Principal,
                    context.Project
                )
            )
            .Select(grant => new CommandArgumentSuggestion(
                    grant.Permission.ToString(),
                    grant.CanDelegate
                        ? CommandDescription(
                            "PermissionCurrentDelegate_Description",
                            "当前为可再授权"
                        )
                        : CommandDescription(
                            "PermissionCurrentUse_Description",
                            "当前为仅使用"
                        )
                )
            );
    }

    private static CommandResult FormatPermissionList(WorldPlayer player)
    {
        var grants = player.Permissions.Grants;
        if (grants.Count == 0)
        {
            return CommandResult.LocalizedOk(
                "permission.list",
                "PermissionListEmpty_Message",
                "{0} 当前没有任何指令权限。",
                player.Address.Value
            );
        }

        var values = grants.Select(grant =>
            $"{grant.Permission} [{(grant.CanDelegate ? "delegate" : "use")}]"
        );
        return CommandResult.LocalizedOk(
            "permission.list",
            "PermissionListHeading_Message",
            "{0} 的指令权限：\n{1}",
            player.Address.Value,
            FormatItems(values)
        );
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

    internal static void SynchronizePermissions(Project project, WorldPlayer player)
    {
        var node = WorldNetworkBinding.For(project).RequireNode();
        var session = node.Clients.Values.FirstOrDefault(client => client.PlayerAddress == player.Address);
        if (session is null) { return; }

        var package = CommandPackage.CreatePermissionSnapshot(player.Address, player.Permissions.Grants);
        NetworkSender.Send(project, package, PackageAudience.To(session));
    }
}
