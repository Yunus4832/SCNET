using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class ServerConnectionIdentityTest
{
    [Fact]
    public void PlayerRosterBindsTheTransportWithoutCreatingAServerPlayer()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        ) { Role = NodeRole.Client, TokenId = Guid.NewGuid() };
        var transport = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        var token = transport.TokenId;
        var character = Guid.NewGuid();
        var roster = new ClientListPackage
        {
            List = [new ClientIdentity(1, node.TokenId, character, null, ClientState.Connected, Guid.NewGuid(), 1)]
        };
        var handler = new ClientPackageHandler();
        handler.Handle(roster, node.CreateReceiveContext(transport));
        Assert.Same(transport, node.Server);
        Assert.Same(node.Clients[1], node.Self);
        Assert.Single(node.Clients);
        Assert.False(node.Clients.ContainsKey(0));
        Assert.Equal(character, node.Self!.CharacterId);
        var self = node.Self;
        handler.Handle(roster, node.CreateReceiveContext(transport));
        Assert.Same(transport, node.Server);
        Assert.Same(self, node.Self);
        Assert.Equal(token, transport.TokenId);
        Assert.True(PackageAudience.To(transport).Includes(node.Server!));
        node.StopImmediate();
    }

    [Fact]
    public void RosterCannotReplaceAnAlreadyBoundAuthorityConnection()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        ) { Role = NodeRole.Client, TokenId = Guid.NewGuid() };
        var transport = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        node.Server = transport;
        var other = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        var roster = new ClientListPackage
        {
            List = [new ClientIdentity(1, node.TokenId, Guid.NewGuid(), null, ClientState.Connected, Guid.NewGuid(), 1)]
        };
        Assert.Throws<InvalidDataException>(() =>
            new ClientPackageHandler().Handle(roster, node.CreateReceiveContext(other))
        );
        Assert.Same(transport, node.Server);
        Assert.Empty(node.Clients);
        node.StopImmediate();
    }

    [Fact]
    public void RosterRejectsAnAuthorityTransportDisguisedAsAPlayer()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        ) { Role = NodeRole.Client, TokenId = Guid.NewGuid() };
        var transport = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        var roster = new ClientListPackage
        {
            List = [new ClientIdentity(0, node.TokenId, Guid.Empty, null, ClientState.Connected, Guid.NewGuid(), 1)]
        };
        Assert.Throws<InvalidDataException>(() =>
            new ClientPackageHandler().Handle(roster, node.CreateReceiveContext(transport))
        );
        Assert.Empty(node.Clients);
        node.StopImmediate();
    }
}
