using Game;
using Game.Commands;

namespace Survivalcraft.Test.Commands;

public class P2PCommandTest
{
    private static readonly ModId _owner = new("game");

    [Fact]
    public void StartUsesExistingConfigurationWithoutEnablingPersistence()
    {
        var current = new P2PRoomSettings
        {
            Name = "World",
            ControlUrl = "http://localhost:31991/",
            ServiceName = "Signal",
            Description = "Description",
            IsPublic = true
        };
        Assert.Null(P2PCommandHandlers.ValidateConfiguration(current, out var resolved));
        Assert.Equal(current, resolved);
        Assert.False(resolved.Enabled);
    }

    [Theory]
    [InlineData("", "Room", "p2p.service_required")]
    [InlineData("http://example.org/", "Room", "p2p.invalid_configuration")]
    [InlineData("ftp://localhost/", "Room", "p2p.invalid_configuration")]
    [InlineData("http://localhost/", "", "p2p.invalid_configuration")]
    public void MissingOrInvalidConfigurationIsRejected(string address, string name, string code)
    {
        Assert.Equal(code, P2PCommandHandlers.ValidateConfiguration(
            new P2PRoomSettings { Name = name, ControlUrl = address }, out _)!.Code);
    }

    [Fact]
    public void PlayerRequiresExplicitPermissionAndAllCommandsRoundTrip()
    {
        var registry = new CommandRegistry();
        BuiltInCommands.Register(registry, _owner);
        registry.Freeze();
        var permission = new ResourceId(_owner, "server.p2p.manage");
        IGameCommand[] commands =
        [
            new StartP2PCommand(),
            new StopP2PCommand(), new GetP2PStatusCommand()
        ];
        foreach (var command in commands)
        {
            Assert.False(registry.CanInvoke(command, CommandPrincipal.CreatePlayer("Host")));
            Assert.False(registry.CanInvoke(command, CommandPrincipal.CreatePlayer("Claimed", permissions: [BuiltInPermissionIds.ManageStandard])));
            Assert.True(registry.CanInvoke(command, CommandPrincipal.CreatePlayer("Authorized", permissions: [permission])));
            Assert.True(registry.CanInvoke(command, CommandPrincipal.LocalOperator));
            Assert.True(registry.CanInvoke(command, CommandPrincipal.System));
            Assert.True(registry.TryEncode(command, out var id, out var data, out _));
            Assert.True(registry.TryDecode(id, data, out var decoded, out _));
            Assert.Equal(command, decoded);
            Assert.True(registry.TryGetDefinition(id, out var definition));
            Assert.Equal(CommandDomain.Server, definition!.Definition.Domain);
            Assert.True(registry.Adapters.TryGet<HttpCommandBinding>(id, out _));
        }
    }
}
