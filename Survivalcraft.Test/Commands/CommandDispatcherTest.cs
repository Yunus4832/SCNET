using Engine.Core;

using Game.Commands;
using Game.Modding;
using Game.Network.Enums;

namespace Survivalcraft.Test.Commands;

public class CommandDispatcherTest
{
    [Fact]
    public void ExecutesMatchingRouteAndParsesTypedArguments()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "scale"),
            new GameCommand(
                "scale",
                "Scale a value",
                [
                    new CommandRoute(
                        [
                            new CommandLiteral("set"),
                            new CommandArgument("value", CommandArgumentKind.Number)
                        ],
                        (_, arguments) => CommandResult.Ok(arguments.Get<double>("value").ToString("0.0")))
                ]));
        registry.Freeze();

        var result = new CommandDispatcher(registry).Execute(
            "/scale set 2.5",
            Context(new CommandPrincipal("Player")));

        Assert.True(result.Success);
        Assert.Equal("2.5", result.Message);
    }

    [Fact]
    public void RejectsUnknownCommandInvalidArgumentsAndMissingPermission()
    {
        var registry = RegistryWithCommand(
            new GameCommand(
                "secure",
                "Secure command",
                [
                    new CommandRoute(
                        [new CommandLiteral("run")],
                        (_, _) => CommandResult.Ok("done"),
                        requiredPermission: "server.secure")
                ]));
        var dispatcher = new CommandDispatcher(registry);

        Assert.Equal("command.unknown", dispatcher.Execute("/missing", Context()).Code);
        Assert.Equal("command.forbidden", dispatcher.Execute("/secure nope", Context()).Code);
        Assert.Equal("command.forbidden", dispatcher.Execute("/secure run", Context()).Code);
        Assert.True(dispatcher.Execute(
            "/secure run",
            Context(new CommandPrincipal("Admin", permissions: ["server.*"]))).Success);
    }

    [Fact]
    public void SupportsQuotedArguments()
    {
        var registry = RegistryWithCommand(
            new GameCommand(
                "echo",
                "Echo text",
                [
                    new CommandRoute(
                        [new CommandArgument("text")],
                        (_, arguments) => CommandResult.Ok(arguments.Get<string>("text")))
                ]));

        var result = new CommandDispatcher(registry).Execute("/echo \"hello world\"", Context());

        Assert.True(result.Success);
        Assert.Equal("hello world", result.Message);
    }

    [Fact]
    public void SuggestionsFollowCommandRoutesAndPermissions()
    {
        var registry = RegistryWithCommand(
            new GameCommand(
                "time",
                "World time",
                [
                    new CommandRoute(
                        [new CommandLiteral("get")],
                        (_, _) => CommandResult.Ok("get")),
                    new CommandRoute(
                        [
                            new CommandLiteral("set"),
                            new CommandArgument(
                                "value",
                                CommandArgumentKind.String,
                                ["day", "night"])
                        ],
                        (_, _) => CommandResult.Ok("set"),
                        requiredPermission: "world.time.set")
                ]));

        Assert.Equal(["get"], registry.Suggest("/time g", new CommandPrincipal("Player"))
            .Select(item => item.Value));
        Assert.Equal(["day", "night"], registry.Suggest(
                "/time set ",
                new CommandPrincipal("Admin", permissions: ["world.*"]))
            .Select(item => item.Value));
        Assert.Empty(registry.Suggest("/time s", new CommandPrincipal("Player")));
    }

    [Fact]
    public void PermissionCommandOnlySuggestsManagementRoutesToDelegators()
    {
        var registry = RegistryWithCommand(BuiltInCommands.CreatePermission());
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
            registry.Suggest("/permission ", player).Select(item => item.Value));
        Assert.Equal(
            ["list"],
            registry.Suggest("/permission ", directWildcard).Select(item => item.Value));
        Assert.Equal(
            ["delegate", "grant", "list", "nodes", "players", "revoke"],
            registry.Suggest("/permission ", delegator).Select(item => item.Value));
        Assert.Equal(
            ["delegate", "grant", "list", "nodes", "players", "revoke"],
            registry.Suggest("/permission ", CommandPrincipal.ServerConsole)
                .Select(item => item.Value));
    }

    [Fact]
    public void DynamicArgumentSuggestionsAreGeneratedAtQueryTime()
    {
        var registry = RegistryWithCommand(
            new GameCommand(
                "choose",
                "Choose player",
                [
                    new CommandRoute(
                        [
                            new CommandArgument(
                                "player",
                                SuggestionProvider: _ =>
                                [
                                    new CommandArgumentSuggestion("Alice Smith", "First player"),
                                    new CommandArgumentSuggestion("Bob", "Second player")
                                ])
                        ],
                        (_, arguments) => CommandResult.Ok(arguments.Get<string>("player")))
                ]));

        var suggestions = registry.Suggest("/choose A", new CommandPrincipal("Player"));

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("Alice Smith", suggestion.Value);
        Assert.Equal("First player", suggestion.Description);
        Assert.Equal("\"Alice Smith\"", CommandLineTokenizer.FormatToken(suggestion.Value));
        Assert.True(CommandLineTokenizer.TryTokenize(
            CommandLineTokenizer.FormatToken(suggestion.Value),
            out var tokens,
            out _));
        Assert.Equal(["Alice Smith"], tokens);
        Assert.Equal(
            "/choose \"Alice Smith\"",
            CommandLineTokenizer.ReplaceCurrentToken(
                "/choose \"Alice S",
                CommandLineTokenizer.FormatToken(suggestion.Value)));
    }

    [Fact]
    public void PermissionNodesAreDerivedFromRegisteredRoutes()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "time"),
            new GameCommand(
                "time",
                "Time",
                [
                    new CommandRoute(
                        [],
                        (_, _) => CommandResult.Ok("ok"),
                        requiredPermission: "world.time.set")
                ]));
        registry.Register(
            owner,
            new ResourceId(owner, "stop"),
            new GameCommand(
                "stop",
                "Stop",
                [
                    new CommandRoute(
                        [],
                        (_, _) => CommandResult.Ok("ok"),
                        requiredPermission: "server.stop")
                ]));
        registry.Freeze();

        Assert.Equal(
            ["*", "server.*", "server.stop", "world.*", "world.time.*", "world.time.set"],
            registry.GetPermissionNodes());
    }

    [Fact]
    public void CanExecuteRequiresACompletePermittedRoute()
    {
        var registry = RegistryWithCommand(
            new GameCommand(
                "time",
                "World time",
                [
                    new CommandRoute(
                        [new CommandLiteral("get")],
                        (_, _) => CommandResult.Ok("get")),
                    new CommandRoute(
                        [
                            new CommandLiteral("set"),
                            new CommandArgument(
                                "value",
                                CommandArgumentKind.String,
                                ["day", "night"])
                        ],
                        (_, _) => CommandResult.Ok("set"),
                        requiredPermission: "world.time.set")
                ]));
        var player = new CommandPrincipal("Player");
        var admin = new CommandPrincipal("Admin", permissions: ["world.*"]);

        Assert.True(registry.CanExecute("/time get ", player));
        Assert.False(registry.CanExecute("/time set ", admin));
        Assert.False(registry.CanExecute("/time set day", player));
        Assert.True(registry.CanExecute("/time set day ", admin));
        Assert.False(registry.CanExecute("/missing", admin));
    }

    [Fact]
    public void CommandEnvironmentDescribesWhereACommandCanRun()
    {
        var stop = new GameCommand(
            "stop",
            "Stop server",
            [new CommandRoute([], (_, _) => CommandResult.Ok("stopped"))],
            executionEnvironment: CommandExecutionEnvironment.HeadlessServer);

        Assert.True(stop.IsAvailable(RunModeType.HeadlessServer, WorkType.Server));
        Assert.False(stop.IsAvailable(RunModeType.Gui, WorkType.Server));
        Assert.False(stop.IsAvailable(RunModeType.Gui, WorkType.Client));
    }

    [Fact]
    public void RegistryRejectsAliasesOwnedByAnotherCommand()
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(
            owner,
            new ResourceId(owner, "first"),
            Command("first", ["f"]));

        var exception = Assert.Throws<InvalidOperationException>(() => registry.Register(
            owner,
            new ResourceId(owner, "second"),
            Command("second", ["f"])));

        Assert.Contains("conflicts", exception.Message);
    }

    [Fact]
    public void HandlerExceptionsBecomeSafeFailures()
    {
        var registry = RegistryWithCommand(
            new GameCommand(
                "fail",
                "Fail",
                [
                    new CommandRoute(
                        [],
                        (_, _) => throw new InvalidOperationException("secret detail"))
                ]));

        var result = new CommandDispatcher(registry).Execute("/fail", Context());

        Assert.False(result.Success);
        Assert.Equal("command.failed", result.Code);
        Assert.DoesNotContain("secret detail", result.Message);
    }

    private static CommandRegistry RegistryWithCommand(GameCommand command)
    {
        var registry = new CommandRegistry();
        var owner = new ModId("example.commands");
        registry.Register(owner, new ResourceId(owner, command.Name), command);
        registry.Freeze();
        return registry;
    }

    private static GameCommand Command(string name, IReadOnlyList<string> aliases)
    {
        return new GameCommand(
            name,
            name,
            [new CommandRoute([], (_, _) => CommandResult.Ok(name))],
            aliases);
    }

    private static CommandContext Context(CommandPrincipal? principal = null)
    {
        return new CommandContext(
            CommandSource.Mod,
            principal ?? new CommandPrincipal("Player"),
            null,
            "test");
    }
}
