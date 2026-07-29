using Engine.Core;

using System.Text.Json.Nodes;

using Game;
using Game.Commands;
using Game.Localization;
using Game.Managers;
using Game.Messaging;
using Game.Modding;
using Game.Network.Enums;

namespace Survivalcraft.Test.Commands;

public class CommandDispatcherTest
{
    [Fact]
    public void TypedDispatcherExecutesRegisteredCommand()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "scale"),
            new CommandDefinition<ScaleCommand>(
                (_, command) => CommandResult.Ok(command.Value.ToString("0.0"))));
        registry.Freeze();

        var result = new CommandDispatcher(registry).Execute(
            new ScaleCommand(2.5),
            Context());

        Assert.True(result.Success);
        Assert.Equal("2.5", result.Message);
    }

    [Fact]
    public void RegistryUsesControlledTypedCommandCodec()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        var id = new ResourceId(owner, "scale");
        registry.Register(
            owner,
            id,
            new CommandDefinition<ScaleCommand>(
                (_, command) => CommandResult.Ok(command.Value.ToString("0.0")),
                write: static (writer, command) => writer.Write(command.Value),
                read: static reader => new ScaleCommand(reader.ReadDouble())));
        registry.Freeze();

        Assert.True(registry.TryEncode(
            new ScaleCommand(2.5),
            out var encodedId,
            out var payload,
            out var encodeError));
        Assert.Equal(string.Empty, encodeError);
        Assert.Equal(id, encodedId);
        Assert.True(registry.TryDecode(
            encodedId,
            payload,
            out var decoded,
            out var decodeError));
        Assert.Equal(string.Empty, decodeError);
        Assert.Equal(new ScaleCommand(2.5), decoded);
    }

    [Fact]
    public void TextAdapterCreatesAndExecutesTypedCommand()
    {
        var registry = RegistryWith(
            new CommandDefinition<ScaleCommand>(
                (_, command) => CommandResult.Ok(command.Value.ToString("0.0"))),
            new TextCommand(
                "scale",
                Text("Scale a value"),
                [
                    new CommandRoute(
                        [
                            new CommandLiteral("set"),
                            new CommandArgument("value", CommandArgumentKind.Number)
                        ],
                        typeof(ScaleCommand),
                        arguments => new ScaleCommand(arguments.Get<double>("value")))
                ]));

        var result = new TextCommandAdapter(registry).Execute(
            "/scale set 2.5",
            Context());

        Assert.True(result.Success);
        Assert.Equal("2.5", result.Message);
    }

    [Fact]
    public void TextAdapterSupportsQuotedArguments()
    {
        var registry = RegistryWith(
            new CommandDefinition<EchoCommand>(
                (_, command) => CommandResult.Ok(command.Text)),
            new TextCommand(
                "echo",
                Text("Echo text"),
                [
                    new CommandRoute(
                        [new CommandArgument("text")],
                        typeof(EchoCommand),
                        arguments => new EchoCommand(arguments.Get<string>("text")))
                ]));

        var result = new TextCommandAdapter(registry).Execute(
            "/echo \"hello world\"",
            Context());

        Assert.True(result.Success);
        Assert.Equal("hello world", result.Message);
    }

    [Fact]
    public void AuthorizationBelongsToTypedDefinition()
    {
        var registry = RegistryWith(
            new CommandDefinition<SecureCommand>(
                (_, _) => CommandResult.Ok("done"),
                requiredPermission: "server.secure"),
            new TextCommand(
                "secure",
                Text("Secure command"),
                [
                    new CommandRoute(
                        [new CommandLiteral("run")],
                        typeof(SecureCommand),
                        _ => new SecureCommand())
                ]));
        var adapter = new TextCommandAdapter(registry);

        Assert.Equal("command.unknown", adapter.Execute("/missing", Context()).Code);
        Assert.Equal("command.forbidden", adapter.Execute("/secure nope", Context()).Code);
        Assert.Equal("command.forbidden", adapter.Execute("/secure run", Context()).Code);
        Assert.True(adapter.Execute(
            "/secure run",
            Context(new CommandPrincipal("Admin", permissions: ["server.*"]))).Success);
    }

    [Fact]
    public void SuggestionsUseTypedCommandAuthorization()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "time/get"),
            new CommandDefinition<GetTimeCommand>((_, _) => CommandResult.Ok("get")));
        registry.Register(
            owner,
            new ResourceId(owner, "time/set"),
            new CommandDefinition<SetTimeCommand>(
                (_, command) => CommandResult.Ok(command.Value),
                requiredPermission: "world.time.set"));
        registry.Adapters.Register(
            owner,
            new ResourceId(owner, "text/time"),
            new TextCommand(
                "time",
                Text("World time"),
                [
                    new CommandRoute(
                        [new CommandLiteral("get")],
                        typeof(GetTimeCommand),
                        _ => new GetTimeCommand()),
                    new CommandRoute(
                        [
                            new CommandLiteral("set"),
                            new CommandArgument(
                                "value",
                                CommandArgumentKind.String,
                                ["day", "night"])
                        ],
                        typeof(SetTimeCommand),
                        arguments => new SetTimeCommand(arguments.Get<string>("value")))
                ]));
        registry.Freeze();
        var textAdapter = new TextCommandAdapter(registry);

        Assert.Equal(
            ["get"],
            textAdapter.Suggest("/time g", new CommandPrincipal("Player"))
                .Select(item => item.Value));
        Assert.Equal(
            ["day", "night"],
            textAdapter.Suggest(
                    "/time set ",
                    new CommandPrincipal("Admin", permissions: ["world.*"]))
                .Select(item => item.Value));
        Assert.Empty(textAdapter.Suggest("/time s", new CommandPrincipal("Player")));
    }

    [Fact]
    public void DynamicArgumentSuggestionsAreGeneratedAtQueryTime()
    {
        var registry = RegistryWith(
            new CommandDefinition<ChooseCommand>(
                (_, command) => CommandResult.Ok(command.Player)),
            new TextCommand(
                "choose",
                Text("Choose player"),
                [
                    new CommandRoute(
                        [
                            new CommandArgument(
                                "player",
                                SuggestionProvider: _ =>
                                [
                                    new CommandArgumentSuggestion(
                                        "Alice Smith",
                                        Text("First player")),
                                    new CommandArgumentSuggestion(
                                        "Bob",
                                        Text("Second player"))
                                ])
                        ],
                        typeof(ChooseCommand),
                        arguments => new ChooseCommand(arguments.Get<string>("player")))
                ]));

        var suggestion = Assert.Single(new TextCommandAdapter(registry)
            .Suggest("/choose A", new CommandPrincipal("Player")));

        Assert.Equal("Alice Smith", suggestion.Value);
        Assert.Equal("First player", suggestion.Description);
        Assert.Equal("\"Alice Smith\"", CommandLineTokenizer.FormatToken(suggestion.Value));
        Assert.Equal(
            "/choose \"Alice Smith\"",
            CommandLineTokenizer.ReplaceCurrentToken(
                "/choose \"Alice S",
                CommandLineTokenizer.FormatToken(suggestion.Value)));
    }

    [Fact]
    public void PermissionNodesAreDerivedFromTypedDefinitions()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "time/set"),
            new CommandDefinition<SetTimeCommand>(
                (_, _) => CommandResult.Ok("ok"),
                requiredPermission: "world.time.set"));
        registry.Register(
            owner,
            new ResourceId(owner, "server/stop"),
            new CommandDefinition<StopCommand>(
                (_, _) => CommandResult.Ok("ok"),
                requiredPermission: "server.stop",
                sourcePolicy: CommandSourcePolicy.ServerConsoleOnly,
                grantPolicy: CommandGrantPolicy.NonGrantable));
        registry.Register(
            owner,
            new ResourceId(owner, "server/kick"),
            new CommandDefinition<KickCommand>(
                (_, _) => CommandResult.Ok("ok"),
                requiredPermission: "server.kick"));
        registry.Freeze();

        Assert.Equal(
            [
                CommandPermissionSet.ManageStandardPermission,
                "server.kick",
                "world.*",
                "world.time.*",
                "world.time.set"
            ],
            registry.GetPermissionNodes());
        var manager = new CommandPrincipal(
            "Manager",
            permissions: [CommandPermissionSet.ManageStandardPermission]);
        Assert.True(registry.CanGrantPermission(
            "world.time.set",
            manager,
            CommandSource.Player));
        Assert.False(registry.CanGrantPermission(
            "server.stop",
            CommandPrincipal.ServerConsole,
            CommandSource.ServerConsole));
        Assert.False(registry.CanGrantPermission(
            "server.kick",
            manager,
            CommandSource.Player));
        Assert.True(registry.CanGrantPermission(
            "server.kick",
            CommandPrincipal.ServerConsole,
            CommandSource.ServerConsole));
    }

    [Fact]
    public void SuggestionsResolveDescriptionsAfterRegistration()
    {
        const string section = "CommandDescriptionTest";
        LanguageManager.KeyWords.TryGetPropertyValue(section, out var existing);
        var original = existing?.DeepClone();
        try
        {
            LanguageManager.KeyWords[section] = JsonNode.Parse(
                """{"TestDescription":"Initial"}""");
            var registry = RegistryWith(
                new CommandDefinition<EchoCommand>(
                    (_, command) => CommandResult.Ok(command.Text)),
                new TextCommand(
                    "echo",
                    new LocalizedText(
                        section,
                        "TestDescription",
                        "Fallback"),
                    [
                        new CommandRoute(
                            [new CommandArgument("text")],
                            typeof(EchoCommand),
                            arguments => new EchoCommand(
                                arguments.Get<string>("text")))
                    ]));
            var adapter = new TextCommandAdapter(registry);

            Assert.Equal(
                "Initial",
                Assert.Single(adapter.Suggest(
                    "/e",
                    new CommandPrincipal("Player"))).Description);

            LanguageManager.KeyWords[section] = JsonNode.Parse(
                """{"TestDescription":"Changed"}""");

            Assert.Equal(
                "Changed",
                Assert.Single(adapter.Suggest(
                    "/e",
                    new CommandPrincipal("Player"))).Description);
        }
        finally
        {
            if (original is null)
            {
                LanguageManager.KeyWords.Remove(section);
            }
            else
            {
                LanguageManager.KeyWords[section] = original;
            }
        }
    }

    [Fact]
    public void CanExecuteRequiresCompletePermittedTextRoute()
    {
        var registry = RegistryWith(
            new CommandDefinition<SetTimeCommand>(
                (_, command) => CommandResult.Ok(command.Value),
                requiredPermission: "world.time.set"),
            new TextCommand(
                "time",
                Text("World time"),
                [
                    new CommandRoute(
                        [
                            new CommandLiteral("set"),
                            new CommandArgument(
                                "value",
                                CommandArgumentKind.String,
                                ["day", "night"])
                        ],
                        typeof(SetTimeCommand),
                        arguments => new SetTimeCommand(arguments.Get<string>("value")))
                ]));
        var player = new CommandPrincipal("Player");
        var admin = new CommandPrincipal("Admin", permissions: ["world.*"]);
        var textAdapter = new TextCommandAdapter(registry);

        Assert.False(textAdapter.CanExecute("/time set ", admin));
        Assert.False(textAdapter.CanExecute("/time set day", player));
        Assert.True(textAdapter.CanExecute("/time set day ", admin));
        Assert.False(textAdapter.CanExecute("/missing", admin));
    }

    [Fact]
    public void SourceAndEnvironmentAreDefinedByTypedCommand()
    {
        var definition = new CommandDefinition<StopCommand>(
            (_, _) => CommandResult.Ok("stopped"),
            requiredPermission: "server.stop",
            sourcePolicy: CommandSourcePolicy.ServerConsoleOnly,
            grantPolicy: CommandGrantPolicy.NonGrantable,
            executionEnvironment: CommandExecutionEnvironment.HeadlessServer);

        Assert.True(definition.IsAvailable(RunModeType.HeadlessServer, WorkType.Server));
        Assert.False(definition.IsAvailable(RunModeType.Gui, WorkType.Server));
        Assert.False(definition.IsSourceAllowed(CommandSource.Player));
        Assert.True(definition.IsSourceAllowed(CommandSource.ServerConsole));
    }

    [Fact]
    public void HandlerExceptionsBecomeSafeFailures()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "fail"),
            new CommandDefinition<FailCommand>(
                (_, _) => throw new InvalidOperationException("secret detail")));
        registry.Freeze();

        var result = new CommandDispatcher(registry).Execute(
            new FailCommand(),
            Context());

        Assert.False(result.Success);
        Assert.Equal("command.failed", result.Code);
        Assert.DoesNotContain("secret detail", result.Message);
    }

    [Fact]
    public void BuiltInTextRoutesFollowTypedSourcePolicies()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("game");
        BuiltInCommands.Register(registry, owner);
        registry.Freeze();
        var textAdapter = new TextCommandAdapter(registry);

        Assert.Equal(
            ["claim"],
            textAdapter.Suggest("/auth ", new CommandPrincipal("Player"))
                .Select(item => item.Value));
        Assert.Equal(
            ["code", "regenerate", "status"],
            textAdapter.Suggest(
                    "/auth ",
                    CommandPrincipal.ServerConsole,
                    CommandSource.ServerConsole)
                .Select(item => item.Value));
        Assert.False(textAdapter.CanExecute(
            "/auth claim ABCD-EFGH-JKLM ",
            CommandPrincipal.ServerConsole,
            CommandSource.ServerConsole));
    }

    [Fact]
    public void BuiltInPlayerListAndRunModeUseAppropriateFrontends()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("game");
        BuiltInCommands.Register(registry, owner);
        registry.Freeze();
        var textAdapter = new TextCommandAdapter(registry);

        Assert.True(textAdapter.CanExecute(
            "/players ",
            new CommandPrincipal("Player")));
        Assert.Empty(textAdapter.Suggest(
            "/runmode",
            new CommandPrincipal("Player")));
        Assert.Equal(
            ["gui", "headless"],
            textAdapter.Suggest(
                    "/runmode ",
                    CommandPrincipal.ServerConsole,
                    CommandSource.ServerConsole)
                .Select(item => item.Value));

        Assert.True(registry.TryEncode(
            new ListPlayersCommand(),
            out var playerListId,
            out var payload,
            out var encodeError));
        Assert.Equal(new ResourceId(owner, "player/list"), playerListId);
        Assert.Equal(string.Empty, encodeError);
        Assert.True(registry.TryDecode(
            playerListId,
            payload,
            out var decodedPlayerList,
            out var decodeError));
        Assert.IsType<ListPlayersCommand>(decodedPlayerList);
        Assert.Equal(string.Empty, decodeError);

        Assert.True(registry.TryGetDefinition<SetRunModeCommand>(out var registered));
        Assert.NotNull(registered);
        Assert.Equal(
            CommandGrantPolicy.NonGrantable,
            registered!.Definition.GrantPolicy);
        Assert.DoesNotContain(
            "server.run_mode.set",
            registry.GetPermissionNodes());

        var forbidden = new CommandDispatcher(registry).Execute(
            new SetRunModeCommand(RunMode.Value),
            new CommandContext(
                CommandSource.Local,
                CommandPrincipal.Local,
                null));
        Assert.False(forbidden.Success);
        Assert.Equal("command.forbidden", forbidden.Code);
        Assert.True(CommandPrincipal.LocalHost.HasPermission(
            "server.run_mode.set"));
        Assert.False(CommandPrincipal.LocalHost.HasPermission(
            "server.stop"));
    }

    [Fact]
    public void BuiltInLanguageCommandUsesLocalOnlyDynamicSuggestions()
    {
        var originalLanguageTypes = LanguageManager.LanguageTypes.ToArray();
        try
        {
            LanguageManager.LanguageTypes.Clear();
            LanguageManager.LanguageTypes.AddRange(["en-US", "zh-CN"]);
            var registry = new CommandRegistry();
            var owner = new ModId("game");
            BuiltInCommands.Register(registry, owner);
            registry.Freeze();
            var textAdapter = new TextCommandAdapter(registry);

            Assert.Empty(textAdapter.Suggest(
                "/language",
                new CommandPrincipal("Player")));
            Assert.Empty(textAdapter.Suggest(
                "/language",
                CommandPrincipal.ServerConsole,
                CommandSource.ServerConsole));
            Assert.Equal(
                ["en-US", "zh-CN"],
                textAdapter.Suggest(
                        "/language ",
                        CommandPrincipal.Local,
                        CommandSource.Local)
                    .Select(item => item.Value));
            Assert.True(textAdapter.CanExecute(
                "/language zh-CN ",
                CommandPrincipal.Local,
                CommandSource.Local));
            Assert.True(textAdapter.SupportsSource(
                "/language unsupported",
                CommandSource.Local));
            Assert.False(textAdapter.SupportsSource(
                "/language unsupported",
                CommandSource.Player));
            Assert.False(textAdapter.SupportsSource(
                "/stop",
                CommandSource.Local));

            Assert.True(registry.TryGetDefinition<SetLanguageCommand>(out var registered));
            Assert.NotNull(registered);
            Assert.Equal(
                CommandSourcePolicy.LocalOnly,
                registered!.Definition.SourcePolicy);
            Assert.Equal(
                CommandGrantPolicy.Standard,
                registered.Definition.GrantPolicy);
            Assert.Equal(string.Empty, registered.Definition.RequiredPermission);
        }
        finally
        {
            LanguageManager.LanguageTypes.Clear();
            LanguageManager.LanguageTypes.AddRange(originalLanguageTypes);
        }
    }

    [Fact]
    public void BuiltInHelpAndSeasonExposeDynamicConstrainedSuggestions()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("game");
        BuiltInCommands.Register(registry, owner);
        registry.Freeze();
        var textAdapter = new TextCommandAdapter(registry);
        var player = new CommandPrincipal(
            "Player",
            permissions: ["world.season.set", "world.weather.*"]);

        var playerHelp = textAdapter.Suggest("/help ", player);
        Assert.Contains(playerHelp, item => item.Value == "help");
        Assert.Contains(playerHelp, item => item.Value == "players");
        Assert.DoesNotContain(playerHelp, item => item.Value == "runmode");

        var consoleHelp = textAdapter.Suggest(
            "/help ",
            CommandPrincipal.ServerConsole,
            CommandSource.ServerConsole);
        Assert.Contains(consoleHelp, item => item.Value == "runmode");

        Assert.Equal(
            ["end", "middle", "start"],
            textAdapter.Suggest("/season set winter ", player)
                .Select(item => item.Value));
        Assert.True(textAdapter.CanExecute(
            "/season set winter middle ",
            player));
        Assert.False(textAdapter.CanExecute(
            "/season set winter 0.5 ",
            player));
        var rainSuggestions = textAdapter.Suggest("/weather rain ", player);
        Assert.Equal(
            ["disable", "enable"],
            rainSuggestions.Select(item => item.Value));
        Assert.NotEqual(
            rainSuggestions.Single(item => item.Value == "enable").Description,
            rainSuggestions.Single(item => item.Value == "disable").Description);
        Assert.Equal(
            ["disable", "enable"],
            textAdapter.Suggest("/weather fog ", player)
                .Select(item => item.Value));
        Assert.True(textAdapter.CanExecute(
            "/weather rain enable ",
            player));
        Assert.False(textAdapter.CanExecute(
            "/weather rain true ",
            player));

        var helpResult = textAdapter.Execute(
            "/help",
            Context(player, CommandSource.Player));
        Assert.True(helpResult.Success);
        Assert.StartsWith("可用指令：\n", helpResult.Message);
        Assert.Contains("\n- /help", helpResult.Message);

        var usageResult = textAdapter.Execute(
            "/season invalid",
            Context(player, CommandSource.Player));
        Assert.Equal("command.usage", usageResult.Code);
        Assert.Contains("\n用法：\n- /season", usageResult.Message);
    }

    [Fact]
    public void PermissionTextRoutesUseTypedDelegationPolicy()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("game");
        BuiltInCommands.Register(registry, owner);
        registry.Freeze();
        var textAdapter = new TextCommandAdapter(registry);
        var player = new CommandPrincipal("Player");
        var delegator = new CommandPrincipal(
            "Delegator",
            permissions: ["world.*"],
            delegablePermissions: ["world.*"]);
        var directWildcard = new CommandPrincipal(
            "DirectWildcard",
            permissions: ["*"]);

        Assert.Equal(
            ["list"],
            textAdapter.Suggest("/permission ", player).Select(item => item.Value));
        Assert.Equal(
            ["list"],
            textAdapter.Suggest("/permission ", directWildcard).Select(item => item.Value));
        Assert.Equal(
            ["delegate", "grant", "list", "nodes", "players", "revoke"],
            textAdapter.Suggest("/permission ", delegator).Select(item => item.Value));
        Assert.Equal(
            ["delegate", "grant", "list", "nodes", "players", "revoke"],
            textAdapter.Suggest(
                    "/permission ",
                    CommandPrincipal.ServerConsole,
                    CommandSource.ServerConsole)
                .Select(item => item.Value));
    }

    [Fact]
    public void AlternativeAuthorizationCanGrantGameplayCapability()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "creative/action"),
            new CommandDefinition<CreativeActionCommand>(
                (_, _) => CommandResult.Ok("done"),
                requiredPermission: "world.creative.action",
                alternativeAuthorization: static (principal, _) =>
                    principal.Name == "CreativePlayer"));
        registry.Freeze();

        Assert.True(registry.CanExecute(
            typeof(CreativeActionCommand),
            new CommandPrincipal("CreativePlayer")));
        Assert.False(registry.CanExecute(
            typeof(CreativeActionCommand),
            new CommandPrincipal("SurvivalPlayer")));
        Assert.True(registry.CanExecute(
            typeof(CreativeActionCommand),
            new CommandPrincipal(
                "Administrator",
                permissions: ["world.creative.*"])));
    }

    [Fact]
    public void BuiltInWorldControlCommandsUseRegisteredBinaryCodecs()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("game");
        BuiltInCommands.Register(registry, owner);
        registry.Freeze();
        IGameCommand[] commands =
        [
            new AdvanceWorldTimeCommand(),
            new SetPrecipitationCommand(true),
            new SetFogCommand(false),
            new TriggerPlayerLightningCommand(),
            new TriggerLightningCommand(
                new Vector3(1f, 2f, 3f),
                new Vector3(0f, 0f, 1f)),
            new SetSeasonCommand(Season.Winter, 0.5f)
        ];

        foreach (var command in commands)
        {
            Assert.True(registry.TryEncode(
                command,
                out var id,
                out var payload,
                out var encodeError));
            Assert.Equal(string.Empty, encodeError);
            Assert.True(registry.TryDecode(
                id,
                payload,
                out var decoded,
                out var decodeError));
            Assert.Equal(string.Empty, decodeError);
            Assert.Equal(command, decoded);
        }
    }

    [Fact]
    public void BuiltInGroupCommandsUseRegisteredBinaryCodecs()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("game");
        BuiltInCommands.Register(registry, owner);
        registry.Freeze();
        IGameCommand[] commands =
        [
            new CreateTeamCommand("Builders"),
            new RequestJoinTeamCommand(Guid.NewGuid()),
            new InvitePlayerToTeamCommand(Guid.NewGuid()),
            new RespondTeamRequestCommand(Guid.NewGuid(), true),
            new LeaveTeamCommand()
        ];

        foreach (var command in commands)
        {
            Assert.True(registry.TryEncode(
                command,
                out var id,
                out var payload,
                out var encodeError));
            Assert.Equal(string.Empty, encodeError);
            Assert.True(registry.TryDecode(
                id,
                payload,
                out var decoded,
                out var decodeError));
            Assert.Equal(string.Empty, decodeError);
            Assert.Equal(command, decoded);
        }
    }

    [Fact]
    public void BuiltInPlayerOperationsUseRegisteredBinaryCodecs()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("game");
        BuiltInCommands.Register(registry, owner);
        registry.Freeze();
        IGameCommand[] commands =
        [
            new UpdateOwnPlayerProfileCommand(
                "Player",
                "$Male1",
                PlayerClass.Male),
            new SendChatMessageCommand(
                GameMessageChannel.Team,
                "hello")
        ];

        foreach (var command in commands)
        {
            Assert.True(registry.TryEncode(
                command,
                out var id,
                out var payload,
                out var encodeError));
            Assert.Equal(string.Empty, encodeError);
            Assert.True(registry.TryDecode(
                id,
                payload,
                out var decoded,
                out var decodeError));
            Assert.Equal(string.Empty, decodeError);
            Assert.Equal(command, decoded);
        }
    }

    [Fact]
    public void PublicResultTargetsAllPlayersWithoutBecomingSensitive()
    {
        var result = CommandResult.PublicOk("changed", "world.changed");

        Assert.True(result.Success);
        Assert.False(result.Sensitive);
        Assert.Equal(CommandResultAudience.AllPlayers, result.Audience);
    }

    [Fact]
    public void SilentResultDoesNotRequireAUserFacingMessage()
    {
        var result = CommandResult.SilentOk("chat.sent");

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.Message);
        Assert.Equal(CommandResultPresentation.Silent, result.Presentation);
    }

    [Fact]
    public void HttpAdapterDispatchesByCommandIdentity()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        var identity = new ResourceId(owner, "scale");
        registry.Register(
            owner,
            identity,
            new CommandDefinition<ScaleCommand>(
                (_, command) => CommandResult.Ok(command.Value.ToString("0.0")),
                requiredPermission: "example.scale",
                sourcePolicy: CommandSourcePolicy.HttpApiOnly));
        registry.Adapters.Register(
            owner,
            identity,
            HttpCommandBinding.Create<ScaleCommand>(
                arguments => new ScaleCommand(arguments.Get<double>("value"))));
        registry.Freeze();
        var context = Context(
            new CommandPrincipal("Http", permissions: ["example.scale"]),
            CommandSource.HttpApi);

        var result = new HttpCommandAdapter(registry).Execute(
            new HttpCommandRequest(
                identity,
                new JsonObject { ["value"] = 2.5 }),
            context);

        Assert.True(result.Success);
        Assert.Equal("2.5", result.Message);
        Assert.Equal("/commands", HttpCommandProtocol.Endpoint);

        var forbidden = new HttpCommandAdapter(registry).Execute(
            new HttpCommandRequest(
                identity,
                new JsonObject { ["value"] = 2.5 }),
            Context(source: CommandSource.HttpApi));
        Assert.False(forbidden.Success);
        Assert.Equal("command.forbidden", forbidden.Code);

        var wrongFrontend = new HttpCommandAdapter(registry).Execute(
            new HttpCommandRequest(
                identity,
                new JsonObject { ["value"] = 2.5 }),
            Context(source: CommandSource.Player));
        Assert.False(wrongFrontend.Success);
        Assert.Equal("command.http_source_required", wrongFrontend.Code);
    }

    [Fact]
    public void HttpAdapterRequiresExplicitIdentityBindingAndPermission()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        var identity = new ResourceId(owner, "scale");
        registry.Register(
            owner,
            identity,
            new CommandDefinition<ScaleCommand>(
                (_, command) => CommandResult.Ok(command.Value.ToString("0.0")),
                requiredPermission: "example.scale",
                sourcePolicy: CommandSourcePolicy.HttpApiOnly));
        registry.Freeze();
        var adapter = new HttpCommandAdapter(registry);

        var hidden = adapter.Execute(
            new HttpCommandRequest(identity, new JsonObject { ["value"] = 2.5 }),
            Context(source: CommandSource.HttpApi));

        Assert.False(hidden.Success);
        Assert.Equal("command.http_not_exposed", hidden.Code);
    }

    [Fact]
    public void HttpBindingIdentityMustMatchRegisteredCommand()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Adapters.Register(
            owner,
            new ResourceId(owner, "missing"),
            HttpCommandBinding.Create<ScaleCommand>(
                arguments => new ScaleCommand(arguments.Get<double>("value"))));

        var exception = Assert.Throws<InvalidOperationException>(registry.Freeze);

        Assert.Contains("same identity", exception.Message);
    }

    [Fact]
    public void TextBindingCanSelectPlayerOrStdinFrontend()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "echo"),
            new CommandDefinition<EchoCommand>(
                (_, command) => CommandResult.Ok(command.Text)));
        registry.Adapters.Register(
            owner,
            new ResourceId(owner, "text/echo"),
            new TextCommand(
                "echo",
                Text("Player-only text adapter"),
                [
                    new CommandRoute(
                        [new CommandArgument("text")],
                        typeof(EchoCommand),
                        arguments => new EchoCommand(arguments.Get<string>("text")))
                ],
                sources: [CommandSource.Player]));
        registry.Freeze();
        var adapter = new TextCommandAdapter(registry);

        var playerResult = adapter.Execute(
            "/echo hello",
            Context(source: CommandSource.Player));
        var stdinResult = adapter.Execute(
            "/echo hello",
            Context(
                CommandPrincipal.ServerConsole,
                CommandSource.ServerConsole));

        Assert.True(playerResult.Success);
        Assert.False(stdinResult.Success);
        Assert.Equal("command.frontend_unavailable", stdinResult.Code);
        Assert.Empty(adapter.Suggest(
            "/echo",
            CommandPrincipal.ServerConsole,
            CommandSource.ServerConsole));
    }

    [Fact]
    public void HttpEnvelopeUsesIdentityInsideOneEndpoint()
    {
        var identity = new ResourceId(new ModId("example.commands"), "scale");
        var envelope = HttpCommandProtocol.CreateEnvelope(
            identity,
            new JsonObject { ["value"] = 2.5 });

        var parsed = HttpCommandProtocol.TryParseEnvelope(
            envelope,
            out var request,
            out var error);

        Assert.True(parsed);
        Assert.Equal(string.Empty, error);
        Assert.Equal(identity, request!.Identity);
        Assert.Equal(2.5, request.Arguments["value"]!.GetValue<double>());
        Assert.Equal("example.commands:scale", envelope["identity"]!.GetValue<string>());
        Assert.Equal("/commands", HttpCommandProtocol.Endpoint);
    }

    private static CommandRegistry RegistryWith<TCommand>(
        CommandDefinition<TCommand> definition,
        TextCommand text)
        where TCommand : IGameCommand
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "typed"),
            definition);
        registry.Adapters.Register(
            owner,
            new ResourceId(owner, "text"),
            text);
        registry.Freeze();
        return registry;
    }

    private static CommandContext Context(
        CommandPrincipal? principal = null,
        CommandSource source = CommandSource.Mod)
    {
        return new CommandContext(
            source,
            principal ?? new CommandPrincipal("Player"),
            null,
            "test");
    }

    private static LocalizedText Text(string value)
    {
        return LocalizedText.Literal(value);
    }

    private sealed record ScaleCommand(double Value) : IGameCommand;

    private sealed record EchoCommand(string Text) : IGameCommand;

    private sealed record SecureCommand : IGameCommand;

    private sealed record GetTimeCommand : IGameCommand;

    private sealed record SetTimeCommand(string Value) : IGameCommand;

    private sealed record ChooseCommand(string Player) : IGameCommand;

    private sealed record StopCommand : IGameCommand;

    private sealed record KickCommand : IGameCommand;

    private sealed record FailCommand : IGameCommand;

    private sealed record CreativeActionCommand : IGameCommand;
}
