using EntitySystem.Core;

using Game.Commands;
using Game.Commands.Http;
using Game.Commands.Text;
using Game.Network;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Subsystems;

namespace Survivalcraft.Test.Commands;

[Collection(NodeRoleCollection.Name)]
public class CommandAuthorityTest
{
    private static readonly ModId _owner = new("test.authority");

    [Theory]
    [InlineData(CommandDomain.Application, true, false)]
    [InlineData(CommandDomain.World, true, true)]
    [InlineData(CommandDomain.Server, true, true)]
    public void ScopeDoesNotChangeLocalOperatorIdentity(
        CommandDomain domain,
        bool localAllowed,
        bool playerAllowed
    )
    {
        var definition = new CommandDefinition<TestCommand>((_, _) => CommandResult.Ok("ok"), domain);

        Assert.Equal(localAllowed, definition.CanInvoke(CommandPrincipal.LocalOperator, null));
        Assert.Equal(playerAllowed, definition.CanInvoke(CommandPrincipal.CreatePlayer("Player"), null));
        Assert.True(definition.CanInvoke(CommandPrincipal.System, null));
    }

    [Theory]
    [InlineData(CommandDomain.Application, NodeRole.Inactive, true)]
    [InlineData(CommandDomain.World, NodeRole.Inactive, false)]
    [InlineData(CommandDomain.Server, NodeRole.Inactive, false)]
    [InlineData(CommandDomain.Application, NodeRole.Authority, true)]
    [InlineData(CommandDomain.Application, NodeRole.Client, true)]
    [InlineData(CommandDomain.World, NodeRole.Authority, true)]
    [InlineData(CommandDomain.World, NodeRole.Client, false)]
    [InlineData(CommandDomain.Server, NodeRole.Authority, true)]
    [InlineData(CommandDomain.Server, NodeRole.Client, false)]
    public void ScopeControlsExecutionEnvironment(
        CommandDomain domain,
        NodeRole worldRole,
        bool expected
    )
    {
        var definition = new CommandDefinition<TestCommand>((_, _) => CommandResult.Ok("ok"), domain);

        using var project = worldRole == NodeRole.Inactive
            ? null
            : new RuntimeTestProject(worldRole == NodeRole.Authority ? RuntimeSide.Authority : RuntimeSide.Client);
        Assert.Equal(expected, definition.CanExecuteHere(project));
    }

    [Fact]
    public void OneDispatcherUsesEachProjectsAuthorityRatherThanAProcessWideRole()
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var replica = new RuntimeTestProject(RuntimeSide.Client);
        var registry = new CommandRegistry();
        var identity = new ResourceId(_owner, "scoped");
        var executions = 0;
        registry.Register(
            _owner,
            identity,
            new CommandDefinition<TestCommand>(
                (_, _) =>
                {
                    executions++;
                    return CommandResult.Ok("executed");
                },
                CommandDomain.World
            )
        );
        registry.Adapters.Register(_owner, identity, HttpCommandBinding.Create(_ => new TestCommand()));
        registry.Freeze();
        var dispatcher = new CommandDispatcher(registry);
        var adapter = new HttpCommandAdapter(registry);

        CommandContext Context(Project? project)
        {
            return new CommandContext(CommandInvocationChannel.Internal, CommandPrincipal.LocalOperator, project);
        }

        Assert.True(dispatcher.Execute(new TestCommand(), Context(authority)).Success);
        Assert.Equal("command.unavailable", dispatcher.Execute(new TestCommand(), Context(replica)).Code);
        Assert.Equal("command.unavailable", dispatcher.Execute(new TestCommand(), Context(null)).Code);
        Assert.Single(
            adapter.Discover(
                CommandPrincipal.LocalOperator,
                replica,
                authority,
                new Game.Localization.LocalizedTextResolver(new Game.Localization.LanguageCatalog())
            )
        );
        Assert.Empty(
            adapter.Discover(
                CommandPrincipal.LocalOperator,
                replica,
                null,
                new Game.Localization.LocalizedTextResolver(new Game.Localization.LanguageCatalog())
            )
        );
        Assert.Empty(
            adapter.Discover(
                CommandPrincipal.LocalOperator,
                null,
                null,
                new Game.Localization.LocalizedTextResolver(new Game.Localization.LanguageCatalog())
            )
        );
        WorldRuntimeContext.For(authority).Close();
        Assert.Equal("command.unavailable", dispatcher.Execute(new TestCommand(), Context(authority)).Code);
        Assert.Equal(1, executions);
    }

    [Fact]
    public void LocalHttpCanManageHostedServerButCannotManageRemoteServer()
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var registry = new CommandRegistry();
        var permission = new ResourceId(_owner, "server.manage");
        var identity = new ResourceId(_owner, "server/manage");
        registry.Permissions.Register(
            _owner,
            permission,
            new CommandPermissionDefinition(CommandDomain.Server)
        );
        registry.Register(
            _owner,
            identity,
            new CommandDefinition<TestCommand>(
                (_, _) => CommandResult.Ok("ok"),
                CommandDomain.Server,
                requiredPermission: permission
            )
        );
        registry.Adapters.Register(
            _owner,
            identity,
            HttpCommandBinding.Create(_ => new TestCommand())
        );
        registry.Freeze();
        var adapter = new HttpCommandAdapter(registry);
        var request = new HttpCommandRequest(identity, new System.Text.Json.Nodes.JsonObject());

        Assert.True(
            adapter.Execute(
                request,
                new CommandContext(CommandInvocationChannel.HttpApi, CommandPrincipal.LocalOperator, authority)
            ).Success
        );
        Assert.False(
            adapter.Execute(
                request,
                new CommandContext(CommandInvocationChannel.HttpApi, CommandPrincipal.CreatePlayer("Player"), authority)
            ).Success
        );
        Assert.True(
            adapter.Execute(
                request,
                new CommandContext(
                    CommandInvocationChannel.HttpApi,
                    CommandPrincipal.CreatePlayer("Authorized player", permissions: [permission]),
                    authority
                )
            ).Success
        );

        Assert.Equal(
            "command.unavailable",
            adapter.Execute(
                request,
                new CommandContext(CommandInvocationChannel.HttpApi, CommandPrincipal.LocalOperator, client)
            ).Code
        );
    }

    [Fact]
    public void PlayerFrontendLocalSuggestionsAreRestrictedToApplicationScope()
    {
        var registry = new CommandRegistry();
        var local = new ResourceId(_owner, "application/manage");
        var server = new ResourceId(_owner, "server/manage");
        registry.Register(
            _owner,
            local,
            new CommandDefinition<TestCommand>((_, _) => CommandResult.Ok("ok"), CommandDomain.Application)
        );
        registry.Register(
            _owner,
            server,
            new CommandDefinition<ServerCommand>((_, _) => CommandResult.Ok("ok"), CommandDomain.Server)
        );
        registry.Adapters.Register(
            _owner,
            new ResourceId(_owner, "text/manage"),
            new TextCommand(
                "manage",
                LocalizedText.Empty,
                [
                    new CommandRoute([new CommandLiteral("application")], typeof(TestCommand), _ => new TestCommand()),
                    new CommandRoute([new CommandLiteral("server")], typeof(ServerCommand), _ => new ServerCommand())
                ]
            )
        );
        registry.Freeze();
        var adapter = new TextCommandAdapter(registry, null);

        Assert.Equal(
            ["application"],
            adapter.Suggest(
                "manage ",
                CommandPrincipal.LocalOperator,
                domain: CommandDomain.Application
            ).Select(suggestion => suggestion.Value)
        );
        Assert.False(
            adapter.CanExecute(
                "manage server",
                CommandPrincipal.LocalOperator,
                domain: CommandDomain.Application
            )
        );
        Assert.True(
            adapter.CanExecute(
                "manage application",
                CommandPrincipal.LocalOperator,
                domain: CommandDomain.Application
            )
        );
    }

    [Fact]
    public void PlayerIdentityCannotSatisfyOperatorRequirement()
    {
        var definition = new CommandDefinition<TestCommand>(
            (_, _) => CommandResult.Ok("ok"),
            CommandDomain.Server,
            allowedPrincipals: CommandPrincipalKinds.LocalOperator | CommandPrincipalKinds.System
        );
        var player = CommandPrincipal.CreatePlayer("Player");

        Assert.False(definition.CanInvoke(player, null));
        Assert.True(definition.CanInvoke(CommandPrincipal.LocalOperator, null));
        Assert.True(definition.CanInvoke(CommandPrincipal.System, null));
    }

    [Fact]
    public void DelegatedPermissionAlsoProvidesUsePermission()
    {
        var permission = new ResourceId(_owner, "server.manage");
        var player = CommandPrincipal.CreatePlayer("Player", delegablePermissions: [permission]);

        Assert.True(player.HasPermission(permission));
        Assert.True(player.CanDelegate(permission));
        Assert.False(player.Is(CommandPrincipalKind.LocalOperator));
    }

    [Fact]
    public void SuggestionsAndImplicitPermissionsUseTheExplicitWorldWithoutACharacter()
    {
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        var controller = typeof(Game.Managers.GameManager).GetField(
            "_worlds",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
        )!.GetValue(null)!;
        var runtimeProperty = typeof(GameWorldController).GetProperty(nameof(GameWorldController.Runtime))!;
        var previous = Game.Managers.GameManager.Runtime;
        using var session = new GameRuntime(false);
        session.Attach(WorldRuntimeContext.For(client));
        try
        {
            runtimeProperty.SetValue(controller, session);
            var registry = new CommandRegistry();
            var permission = new ResourceId(_owner, "world.suggest");
            registry.Permissions.Register(
                _owner,
                permission,
                new CommandPermissionDefinition(
                    CommandDomain.World,
                    PermissionGrantPolicy.NonGrantable,
                    implicitGrant: (_, project) => ReferenceEquals(project, authority)
                )
            );
            registry.Register(
                _owner,
                new ResourceId(_owner, "suggest"),
                new CommandDefinition<TestCommand>(
                    (_, _) => CommandResult.Ok("ok"),
                    CommandDomain.World,
                    requiredPermission: permission
                )
            );
            registry.Adapters.Register(
                _owner,
                new ResourceId(_owner, "text/suggest"),
                new TextCommand(
                    "suggest",
                    LocalizedText.Empty,
                    [
                        new CommandRoute(
                            [
                                new CommandArgument(
                                    "world",
                                    SuggestionProvider: context =>
                                    [
                                        new CommandArgumentSuggestion(
                                            ReferenceEquals(context.Project, authority) ? "authority" : "wrong"
                                        )
                                    ]
                                )
                            ],
                            typeof(TestCommand),
                            _ => new TestCommand()
                        )
                    ]
                )
            );
            registry.Freeze();
            var principal = CommandPrincipal.CreatePlayer("AdmittedWithoutCharacter");
            var adapter = new TextCommandAdapter(registry, authority);

            Assert.True(registry.CanInvoke(typeof(TestCommand), principal, authority));
            Assert.False(registry.CanInvoke(typeof(TestCommand), principal, client));
            Assert.False(registry.CanInvoke(typeof(TestCommand), principal, null));
            Assert.Equal("authority", Assert.Single(adapter.Suggest("/suggest ", principal)).Value);
        }
        finally
        {
            runtimeProperty.SetValue(controller, previous);
        }
    }

    private sealed record TestCommand : IGameCommand;

    private sealed record ServerCommand : IGameCommand;
}
