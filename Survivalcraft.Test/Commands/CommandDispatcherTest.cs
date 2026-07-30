using Engine.Core;

using System.Text.Json.Nodes;

using Game;
using Game.Commands;
using Game.Localization;
using Game.Managers;
using Game.Messaging;
using Game.Modding;

namespace Survivalcraft.Test.Commands;

public class CommandDispatcherTest
{
    private static readonly ModId _owner = new("example.commands");

    [Fact]
    public void TypedDispatcherExecutesRegisteredWorldCommand()
    {
        var registry = new CommandRegistry();
        registry.Register(
            _owner,
            Id("scale"),
            new CommandDefinition<ScaleCommand>(
                (_, command) => CommandResult.Ok(command.Value.ToString("0.0")),
                CommandDomain.World));
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
        var identity = Id("scale");
        registry.Register(
            _owner,
            identity,
            new CommandDefinition<ScaleCommand>(
                (_, command) => CommandResult.Ok(command.Value.ToString("0.0")),
                CommandDomain.World,
                write: static (writer, command) => writer.Write(command.Value),
                read: static reader => new ScaleCommand(reader.ReadDouble())));
        registry.Freeze();

        Assert.True(registry.TryEncode(
            new ScaleCommand(2.5),
            out var encodedId,
            out var payload,
            out var encodeError));
        Assert.Equal(string.Empty, encodeError);
        Assert.Equal(identity, encodedId);
        Assert.True(registry.TryDecode(
            encodedId,
            payload,
            out var decoded,
            out var decodeError));
        Assert.Equal(string.Empty, decodeError);
        Assert.Equal(new ScaleCommand(2.5), decoded);
    }

    [Fact]
    public void TextAdapterCreatesTypedCommandAndSupportsQuotedArguments()
    {
        var registry = RegistryWith(
            new CommandDefinition<EchoCommand>(
                (_, command) => CommandResult.Ok(command.Text),
                CommandDomain.World),
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
    public void PermissionMustBeExplicitlyRegisteredAndMatchCommandDomain()
    {
        var permission = Id("world.time.set");
        var missing = new CommandRegistry();
        missing.Register(
            _owner,
            Id("time/set"),
            new CommandDefinition<SetTimeCommand>(
                (_, command) => CommandResult.Ok(command.Value),
                CommandDomain.World,
                requiredPermission: permission));

        Assert.Throws<InvalidOperationException>(missing.Freeze);

        var wrongDomain = new CommandRegistry();
        wrongDomain.Permissions.Register(
            _owner,
            permission,
            new CommandPermissionDefinition(CommandDomain.Server));
        wrongDomain.Register(
            _owner,
            Id("time/set"),
            new CommandDefinition<SetTimeCommand>(
                (_, command) => CommandResult.Ok(command.Value),
                CommandDomain.World,
                requiredPermission: permission));

        Assert.Throws<InvalidOperationException>(wrongDomain.Freeze);
    }

    [Fact]
    public void SuggestionsAndExecutionUseTheSamePermissionEvaluator()
    {
        var permission = Id("world.time.set");
        var registry = new CommandRegistry();
        registry.Permissions.Register(
            _owner,
            permission,
            new CommandPermissionDefinition(CommandDomain.World));
        registry.Register(
            _owner,
            Id("time/get"),
            new CommandDefinition<GetTimeCommand>(
                (_, _) => CommandResult.Ok("get"),
                CommandDomain.World));
        registry.Register(
            _owner,
            Id("time/set"),
            new CommandDefinition<SetTimeCommand>(
                (_, command) => CommandResult.Ok(command.Value),
                CommandDomain.World,
                requiredPermission: permission));
        registry.Adapters.Register(
            _owner,
            Id("text/time"),
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
                                Choices: ["day", "night"])
                        ],
                        typeof(SetTimeCommand),
                        arguments => new SetTimeCommand(
                            arguments.Get<string>("value")))
                ]));
        registry.Freeze();
        var adapter = new TextCommandAdapter(registry);
        var player = Player("Player");
        var administrator = Player("Administrator", [permission]);

        Assert.Equal(
            ["get"],
            adapter.Suggest("/time ", player).Select(item => item.Value));
        Assert.Equal(
            ["get", "set"],
            adapter.Suggest("/time ", administrator).Select(item => item.Value));
        Assert.Equal(
            "command.forbidden",
            adapter.Execute("/time set day", Context(player)).Code);
        Assert.True(adapter.Execute(
            "/time set day",
            Context(administrator)).Success);
    }

    [Fact]
    public void DynamicArgumentSuggestionsAreGeneratedAtQueryTime()
    {
        var registry = RegistryWith(
            new CommandDefinition<ChooseCommand>(
                (_, command) => CommandResult.Ok(command.Player),
                CommandDomain.World),
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
                        arguments => new ChooseCommand(
                            arguments.Get<string>("player")))
                ]));

        var suggestion = Assert.Single(new TextCommandAdapter(registry)
            .Suggest("/choose A", Player("Player")));

        Assert.Equal("Alice Smith", suggestion.Value);
        Assert.Equal("First player", suggestion.Description);
        Assert.Equal(
            "\"Alice Smith\"",
            CommandLineTokenizer.FormatToken(suggestion.Value));
    }

    [Fact]
    public void ImplicitPermissionBelongsToPermissionDefinition()
    {
        var permission = Id("world.creative.action");
        var registry = new CommandRegistry();
        registry.Permissions.Register(
            _owner,
            permission,
            new CommandPermissionDefinition(
                CommandDomain.World,
                PermissionGrantPolicy.OperatorOnly,
                implicitGrant: static (principal, _) =>
                    principal.Name == "CreativePlayer"));
        registry.Register(
            _owner,
            Id("creative/action"),
            new CommandDefinition<CreativeActionCommand>(
                (_, _) => CommandResult.Ok("done"),
                CommandDomain.World,
                requiredPermission: permission));
        registry.Freeze();

        Assert.True(registry.CanInvoke(
            typeof(CreativeActionCommand),
            Player("CreativePlayer")));
        Assert.False(registry.CanInvoke(
            typeof(CreativeActionCommand),
            Player("SurvivalPlayer")));
        Assert.True(registry.CanInvoke(
            typeof(CreativeActionCommand),
            CommandPrincipal.ServerOperator));
    }

    [Fact]
    public void CommandDomainsSeparateRoutingFromFrontend()
    {
        var application = new CommandDefinition<GetLanguageCommand>(
            (_, _) => CommandResult.Ok("language"),
            CommandDomain.Application);
        var world = new CommandDefinition<GetTimeCommand>(
            (_, _) => CommandResult.Ok("time"),
            CommandDomain.World);
        var server = new CommandDefinition<StopCommand>(
            (_, _) => CommandResult.Ok("stop"),
            CommandDomain.Server,
            hostRequirement: CommandHostRequirement.HeadlessServer);

        Assert.True(application.CanInvoke(CommandPrincipal.ApplicationUser, null));
        Assert.False(application.CanInvoke(Player("Player"), null));
        Assert.True(world.CanInvoke(Player("Player"), null));
        Assert.False(server.CanInvoke(CommandPrincipal.ApplicationUser, null));
        Assert.False(server.CanInvoke(CommandPrincipal.ServerOperator, null));
    }

    [Fact]
    public void TextRoutingResolvesTheMatchedRouteDomain()
    {
        var registry = new CommandRegistry();
        registry.Register(
            _owner,
            Id("application/action"),
            new CommandDefinition<ApplicationActionCommand>(
                (_, _) => CommandResult.Ok("application"),
                CommandDomain.Application));
        registry.Register(
            _owner,
            Id("world/action"),
            new CommandDefinition<WorldActionCommand>(
                (_, _) => CommandResult.Ok("world"),
                CommandDomain.World));
        registry.Adapters.Register(
            _owner,
            Id("text/action"),
            new TextCommand(
                "action",
                Text("Action"),
                [
                    new CommandRoute(
                        [new CommandLiteral("application")],
                        typeof(ApplicationActionCommand),
                        _ => new ApplicationActionCommand()),
                    new CommandRoute(
                        [new CommandLiteral("world")],
                        typeof(WorldActionCommand),
                        _ => new WorldActionCommand())
                ]));
        registry.Freeze();
        var adapter = new TextCommandAdapter(registry);

        Assert.True(adapter.SupportsDomain(
            "/action application",
            CommandDomain.Application));
        Assert.False(adapter.SupportsDomain(
            "/action world",
            CommandDomain.Application));
        Assert.True(adapter.SupportsDomain(
            "/action world",
            CommandDomain.World));
    }

    [Fact]
    public void HandlerExceptionsBecomeSafeFailures()
    {
        var registry = new CommandRegistry();
        registry.Register(
            _owner,
            Id("fail"),
            new CommandDefinition<FailCommand>(
                (_, _) => throw new InvalidOperationException("secret detail"),
                CommandDomain.World));
        registry.Freeze();

        var result = new CommandDispatcher(registry).Execute(
            new FailCommand(),
            Context());

        Assert.False(result.Success);
        Assert.Equal("command.failed", result.Code);
        Assert.DoesNotContain("secret detail", result.Message);
    }

    [Fact]
    public void BuiltInCommandsDeclareExpectedDomainsAndPermissionPolicies()
    {
        var registry = BuiltInRegistry();

        AssertDomain<GetLanguageCommand>(registry, CommandDomain.Application);
        AssertDomain<SetRunModeCommand>(registry, CommandDomain.Application);
        AssertDomain<SetWorldTimeCommand>(registry, CommandDomain.World);
        AssertDomain<CreateTeamCommand>(registry, CommandDomain.World);
        AssertDomain<StopServerCommand>(registry, CommandDomain.Server);
        AssertDomain<GrantPlayerPermissionCommand>(registry, CommandDomain.Server);

        Assert.True(registry.TryGetDefinition<StopServerCommand>(out var stop));
        var stopPermission = Assert.IsType<ResourceId>(
            stop!.Definition.RequiredPermission);
        Assert.True(registry.Permissions.TryGet(stopPermission, out var registered));
        Assert.Equal(
            PermissionGrantPolicy.OperatorOnly,
            registered!.Definition.GrantPolicy);
    }

    [Fact]
    public void BuiltInApplicationAndWorldSuggestionsAreSeparatedByPrincipal()
    {
        var originalLanguageTypes = LanguageManager.LanguageTypes.ToArray();
        try
        {
            LanguageManager.LanguageTypes.Clear();
            LanguageManager.LanguageTypes.AddRange(["en-US", "zh-CN"]);
            var adapter = new TextCommandAdapter(BuiltInRegistry());

            Assert.Equal(
                ["en-US", "zh-CN"],
                adapter.Suggest(
                        "/language ",
                        CommandPrincipal.ApplicationUser)
                    .Select(item => item.Value));
            Assert.Empty(adapter.Suggest("/language", Player("Player")));
            Assert.Empty(adapter.Suggest(
                "/time",
                CommandPrincipal.ApplicationUser));
            Assert.Contains(
                adapter.Suggest("/", Player("Player")),
                item => item.Value == "time");
        }
        finally
        {
            LanguageManager.LanguageTypes.Clear();
            LanguageManager.LanguageTypes.AddRange(originalLanguageTypes);
        }
    }

    [Fact]
    public void BuiltInSeasonAndWeatherSuggestionsRemainConstrained()
    {
        var owner = new ModId("game");
        var player = Player(
            "Player",
            [
                new ResourceId(owner, "world.season.set"),
                new ResourceId(owner, "world.weather.precipitation.set"),
                new ResourceId(owner, "world.weather.fog.set")
            ]);
        var adapter = new TextCommandAdapter(BuiltInRegistry());

        var seasonSuggestions = adapter.Suggest("/season set ", player);
        Assert.Equal(
            ["autumn", "spring", "summer", "winter"],
            seasonSuggestions.Select(item => item.Value));
        Assert.Equal(
            seasonSuggestions.Count,
            seasonSuggestions.Select(item => item.Description).Distinct().Count());
        var seasonProgressSuggestions =
            adapter.Suggest("/season set winter ", player);
        Assert.Equal(
            ["end", "middle", "start"],
            seasonProgressSuggestions.Select(item => item.Value));
        Assert.Equal(
            seasonProgressSuggestions.Count,
            seasonProgressSuggestions
                .Select(item => item.Description)
                .Distinct()
                .Count());
        Assert.True(adapter.CanExecute(
            "/season set winter middle ",
            player));
        Assert.False(adapter.CanExecute(
            "/season set winter 0.5 ",
            player));
        var weatherSuggestions = adapter.Suggest("/weather rain ", player);
        Assert.Equal(
            ["disable", "enable"],
            weatherSuggestions.Select(item => item.Value));
        Assert.Equal(
            weatherSuggestions.Count,
            weatherSuggestions.Select(item => item.Description).Distinct().Count());
        Assert.False(adapter.CanExecute(
            "/weather rain true ",
            player));
    }

    [Fact]
    public void BuiltInWorldAndGroupCommandsUseRegisteredBinaryCodecs()
    {
        var registry = BuiltInRegistry();
        IGameCommand[] commands =
        [
            new AdvanceWorldTimeCommand(),
            new SetPrecipitationCommand(true),
            new SetFogCommand(false),
            new TriggerPlayerLightningCommand(),
            new TriggerLightningCommand(
                new Vector3(1f, 2f, 3f),
                new Vector3(0f, 0f, 1f)),
            new SetSeasonCommand(Season.Winter, 0.5f),
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
        var registry = BuiltInRegistry();
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
    public void CommandResultPresentationKeepsAudienceAndSilenceDistinct()
    {
        var publicResult = CommandResult.PublicOk(
            "changed",
            "world.changed");
        var silentResult = CommandResult.SilentOk("chat.sent");

        Assert.Equal(
            CommandResultAudience.AllPlayers,
            publicResult.Audience);
        Assert.False(publicResult.Sensitive);
        Assert.Equal(string.Empty, silentResult.Message);
        Assert.Equal(
            CommandResultPresentation.Silent,
            silentResult.Presentation);
    }

    [Fact]
    public void HttpAdapterDispatchesByIdentityWithoutOwningAuthorization()
    {
        var permission = Id("world.scale");
        var identity = Id("scale");
        var registry = new CommandRegistry();
        registry.Permissions.Register(
            _owner,
            permission,
            new CommandPermissionDefinition(CommandDomain.World));
        registry.Register(
            _owner,
            identity,
            new CommandDefinition<ScaleCommand>(
                (_, command) => CommandResult.Ok(command.Value.ToString("0.0")),
                CommandDomain.World,
                requiredPermission: permission));
        registry.Adapters.Register(
            _owner,
            identity,
            HttpCommandBinding.Create<ScaleCommand>(
                arguments => new ScaleCommand(arguments.Get<double>("value"))));
        registry.Freeze();
        var adapter = new HttpCommandAdapter(registry);

        var result = adapter.Execute(
            new HttpCommandRequest(
                identity,
                new JsonObject { ["value"] = 2.5 }),
            Context(
                Player("Http", [permission]),
                CommandInvocationChannel.HttpApi));

        Assert.True(result.Success);
        Assert.Equal("2.5", result.Message);
        Assert.Equal("/commands", HttpCommandProtocol.Endpoint);
        Assert.Equal(
            "command.forbidden",
            adapter.Execute(
                new HttpCommandRequest(
                    identity,
                    new JsonObject { ["value"] = 2.5 }),
                Context(channel: CommandInvocationChannel.HttpApi)).Code);
    }

    [Fact]
    public void HttpAdapterRequiresAnExplicitBinding()
    {
        var identity = Id("scale");
        var registry = new CommandRegistry();
        registry.Register(
            _owner,
            identity,
            new CommandDefinition<ScaleCommand>(
                (_, command) => CommandResult.Ok(
                    command.Value.ToString("0.0")),
                CommandDomain.World));
        registry.Freeze();

        var result = new HttpCommandAdapter(registry).Execute(
            new HttpCommandRequest(
                identity,
                new JsonObject { ["value"] = 2.5 }),
            Context(channel: CommandInvocationChannel.HttpApi));

        Assert.Equal("command.http_not_exposed", result.Code);
    }

    [Fact]
    public void HttpBindingIdentityMustMatchRegisteredCommand()
    {
        var registry = new CommandRegistry();
        registry.Adapters.Register(
            _owner,
            Id("missing"),
            HttpCommandBinding.Create<ScaleCommand>(
                arguments => new ScaleCommand(
                    arguments.Get<double>("value"))));

        var exception = Assert.Throws<InvalidOperationException>(
            registry.Freeze);

        Assert.Contains("same identity", exception.Message);
    }

    [Fact]
    public void FrontendBindingDoesNotRestrictCommandAuthority()
    {
        var registry = RegistryWith(
            new CommandDefinition<EchoCommand>(
                (_, command) => CommandResult.Ok(command.Text),
                CommandDomain.World),
            new TextCommand(
                "echo",
                Text("Echo"),
                [
                    new CommandRoute(
                        [new CommandArgument("text")],
                        typeof(EchoCommand),
                        arguments => new EchoCommand(
                            arguments.Get<string>("text")))
                ]));
        var adapter = new TextCommandAdapter(registry);

        Assert.True(adapter.Execute(
            "/echo player",
            Context(Player("Player"), CommandInvocationChannel.Text)).Success);
        Assert.True(adapter.Execute(
            "/echo console",
            Context(
                CommandPrincipal.ServerOperator,
                CommandInvocationChannel.ServerControl)).Success);
    }

    [Fact]
    public void HttpEnvelopeUsesIdentityInsideOneEndpoint()
    {
        var identity = Id("scale");
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
        Assert.Equal("/commands", HttpCommandProtocol.Endpoint);
    }

    [Fact]
    public void DynamicDescriptionsResolveAfterRegistration()
    {
        const string section = "CommandDescriptionTest";
        LanguageManager.KeyWords.TryGetPropertyValue(section, out var existing);
        var original = existing?.DeepClone();
        try
        {
            LanguageManager.KeyWords[section] =
                JsonNode.Parse("""{"Description":"Initial"}""");
            var registry = RegistryWith(
                new CommandDefinition<EchoCommand>(
                    (_, command) => CommandResult.Ok(command.Text),
                    CommandDomain.World),
                new TextCommand(
                    "echo",
                    new LocalizedText(section, "Description", "Fallback"),
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
                    Player("Player"))).Description);
            LanguageManager.KeyWords[section] =
                JsonNode.Parse("""{"Description":"Changed"}""");
            Assert.Equal(
                "Changed",
                Assert.Single(adapter.Suggest(
                    "/e",
                    Player("Player"))).Description);
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

    private static CommandRegistry RegistryWith<TCommand>(
        CommandDefinition<TCommand> definition,
        TextCommand text)
        where TCommand : IGameCommand
    {
        var registry = new CommandRegistry();
        registry.Register(_owner, Id("typed"), definition);
        registry.Adapters.Register(_owner, Id("text"), text);
        registry.Freeze();
        return registry;
    }

    private static CommandRegistry BuiltInRegistry()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("game");
        BuiltInCommands.Register(registry, owner);
        registry.Freeze();
        return registry;
    }

    private static void AssertDomain<TCommand>(
        CommandRegistry registry,
        CommandDomain expected)
        where TCommand : IGameCommand
    {
        Assert.True(registry.TryGetDefinition<TCommand>(out var registered));
        Assert.Equal(expected, registered!.Definition.Domain);
    }

    private static CommandContext Context(
        CommandPrincipal? principal = null,
        CommandInvocationChannel channel = CommandInvocationChannel.Mod)
    {
        return new CommandContext(
            channel,
            principal ?? Player("Player"),
            null,
            "test");
    }

    private static CommandPrincipal Player(
        string name,
        IEnumerable<ResourceId>? permissions = null,
        IEnumerable<ResourceId>? delegablePermissions = null)
    {
        return new CommandPrincipal(
            name,
            CommandPrincipalKind.Player,
            permissions: permissions,
            delegablePermissions: delegablePermissions);
    }

    private static ResourceId Id(string path) => new(_owner, path);

    private static LocalizedText Text(string value) =>
        LocalizedText.Literal(value);

    private sealed record ScaleCommand(double Value) : IGameCommand;

    private sealed record EchoCommand(string Text) : IGameCommand;

    private sealed record GetTimeCommand : IGameCommand;

    private sealed record SetTimeCommand(string Value) : IGameCommand;

    private sealed record ChooseCommand(string Player) : IGameCommand;

    private sealed record StopCommand : IGameCommand;

    private sealed record FailCommand : IGameCommand;

    private sealed record CreativeActionCommand : IGameCommand;

    private sealed record ApplicationActionCommand : IGameCommand;

    private sealed record WorldActionCommand : IGameCommand;
}
