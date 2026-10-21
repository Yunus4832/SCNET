using Engine.Core.Runtime;

using Game.Commands;
using Game.Network;

namespace Survivalcraft.Test.Commands;

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
    [InlineData(CommandDomain.Application, WorkType.Local, true)]
    [InlineData(CommandDomain.Application, WorkType.Client, true)]
    [InlineData(CommandDomain.World, WorkType.Local, true)]
    [InlineData(CommandDomain.World, WorkType.Client, false)]
    [InlineData(CommandDomain.Server, WorkType.Local, false)]
    [InlineData(CommandDomain.Server, WorkType.Client, false)]
    [InlineData(CommandDomain.Server, WorkType.Server, true)]
    public void ScopeControlsExecutionEnvironment(
        CommandDomain domain,
        WorkType workType,
        bool expected
    )
    {
        var definition = new CommandDefinition<TestCommand>((_, _) => CommandResult.Ok("ok"), domain);

        Assert.Equal(expected, definition.CanExecuteHere(RunModeType.Gui, workType));
    }

    [Fact]
    public void LocalHttpCanManageHostedServerButCannotManageRemoteServer()
    {
        var previous = CommonLib.WorkType;
        try
        {
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
            CommonLib.WorkType = WorkType.Server;

            Assert.True(
                adapter.Execute(
                    request,
                    new CommandContext(CommandInvocationChannel.HttpApi, CommandPrincipal.LocalOperator, null)
                ).Success
            );
            Assert.False(
                adapter.Execute(
                    request,
                    new CommandContext(CommandInvocationChannel.HttpApi, CommandPrincipal.CreatePlayer("Player"), null)
                ).Success
            );
            Assert.True(
                adapter.Execute(
                    request,
                    new CommandContext(
                        CommandInvocationChannel.HttpApi,
                        CommandPrincipal.CreatePlayer("Authorized player", permissions: [permission]),
                        null
                    )
                ).Success
            );

            CommonLib.WorkType = WorkType.Client;
            Assert.Equal(
                "command.unavailable",
                adapter.Execute(
                    request,
                    new CommandContext(CommandInvocationChannel.HttpApi, CommandPrincipal.LocalOperator, null)
                ).Code
            );
        }
        finally
        {
            CommonLib.WorkType = previous;
        }
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
        var adapter = new TextCommandAdapter(registry);

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

    private sealed record TestCommand : IGameCommand;

    private sealed record ServerCommand : IGameCommand;
}
