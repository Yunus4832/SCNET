using Engine.Core.Security;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Protocol.Identity;

namespace Survivalcraft.Test.Network;

public sealed class ControllerLifetimeTest
{
    [Fact]
    public void BulkEntityMetadataCannotRollBackControlOwnedState()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var authority = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        node.Server = authority;
        var source = new CharacterControllerSnapshot(
            1,
            Guid.NewGuid(),
            null,
            ClientState.LoadTerrain,
            Guid.NewGuid(),
            1
        );
        Assert.True(ClientPackageHandler.InstallController(node, source));
        new ClientPackageHandler().Handle(
            new ClientStateChangedPackage
            {
                ClientId = 1, State = ClientState.Playing, ConnectionEpoch = source.ConnectionEpoch,
                ConnectionGeneration = 1
            },
            node.CreateReceiveContext(authority)
        );
        Assert.True(ClientPackageHandler.InstallController(node, source));
        Assert.True(ClientPackageHandler.InstallController(node, source with { State = ClientState.NotConnected }));
        Assert.Equal(ClientState.Playing, node.Connections.Members[1].State);
        node.StopImmediate();
    }

    [Fact]
    public void InitialSelfLifetimeCanOnlyBeInstalledByAuthenticatedRoster()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        ) { Role = NodeRole.Client, TokenId = Guid.NewGuid() };
        var self = new Client(null, 1, node.TokenId, Guid.Empty);
        node.Self = self;
        var snapshot = new CharacterControllerSnapshot(1, Guid.Empty, null, ClientState.Connected, Guid.NewGuid(), 1);
        Assert.False(ClientPackageHandler.InstallController(node, snapshot));
        Assert.Equal(0, self.ConnectionGeneration);
        var authority = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        node.Server = authority;
        new ClientPackageHandler().Handle(
            new ClientListPackage
            {
                List =
                [
                    new ClientIdentity(
                        1,
                        node.TokenId,
                        Guid.Empty,
                        null,
                        ClientState.Connected,
                        snapshot.ConnectionEpoch,
                        1
                    )
                ]
            },
            node.CreateReceiveContext(authority)
        );
        Assert.Same(self, node.Connections.Members[1]);
        Assert.Equal(1, self.ConnectionGeneration);
        node.StopImmediate();
    }

    [Fact]
    public void RosterAddAndRemoveUseTheSameReliableOrderedControlChannel()
    {
        var add = PackageTransportPolicy.Get(new ClientAddedPackage());
        var remove = PackageTransportPolicy.Get(new ClientRemovedPackage());
        Assert.Equal(PackageTransportPolicy.Control, add);
        Assert.Equal(PackageTransportPolicy.Control, remove);
    }

    [Fact]
    public void EntityMetadataThenAuthenticatedRosterConvergesWithoutInventingPhysicalClients()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        ) { Role = NodeRole.Client, TokenId = Guid.NewGuid() };
        var authority = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        node.Server = authority;
        var address = new PlayerAddress(IdentityAddress.Encode(new byte[32]));
        var remote = new CharacterControllerSnapshot(
            1,
            Guid.NewGuid(),
            address,
            ClientState.Playing,
            Guid.NewGuid(),
            1
        );
        Assert.True(ClientPackageHandler.InstallController(node, remote));
        var installed = node.Connections.Members[1];
        Assert.IsNotType<Client>(installed);
        Assert.True(installed.IsConnected);
        var handler = new ClientPackageHandler();
        var remoteIdentity = new ClientIdentity(
            1,
            Guid.NewGuid(),
            remote.CharacterId,
            address,
            remote.State,
            remote.ConnectionEpoch,
            remote.ConnectionGeneration
        );
        handler.Handle(new ClientAddedPackage { Client = remoteIdentity }, node.CreateReceiveContext(authority));
        Assert.Same(installed, node.Connections.Members[1]);
        Assert.Empty(node.Clients);
        var ownAddress = new PlayerAddress(IdentityAddress.Encode(Enumerable.Repeat((byte)1, 32).ToArray()));
        handler.Handle(
            new ClientListPackage
            {
                List =
                [
                    remoteIdentity,
                    new ClientIdentity(
                        2,
                        node.TokenId,
                        Guid.Empty,
                        ownAddress,
                        ClientState.Connected,
                        Guid.NewGuid(),
                        2
                    )
                ]
            },
            node.CreateReceiveContext(authority)
        );
        Assert.Same(installed, node.Connections.Members[1]);
        Assert.Single(node.Clients);
        Assert.Equal(2, node.Connections.PlayerCount);
        Assert.Same(node.Self, node.Connections.Self);
        node.StopImmediate();
    }

    [Fact]
    public void StaleStateAndRemovalCannotTouchReusedSlotOrResurrectTombstone()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var authority = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        node.Server = authority;
        var first = new CharacterControllerSnapshot(1, Guid.NewGuid(), null, ClientState.Connected, Guid.NewGuid(), 1);
        Assert.True(ClientPackageHandler.InstallController(node, first));
        var newer = first with
        {
            CharacterId = Guid.NewGuid(), ConnectionEpoch = Guid.NewGuid(), ConnectionGeneration = 2
        };
        Assert.True(ClientPackageHandler.InstallController(node, newer));
        var handler = new ClientPackageHandler();
        handler.Handle(
            new ClientStateChangedPackage
            {
                ClientId = 1, State = ClientState.NotConnected, ConnectionEpoch = first.ConnectionEpoch,
                ConnectionGeneration = 1
            },
            node.CreateReceiveContext(authority)
        );
        handler.Handle(
            new ClientRemovedPackage
                { ClientId = 1, ConnectionEpoch = first.ConnectionEpoch, ConnectionGeneration = 1 },
            node.CreateReceiveContext(authority)
        );
        Assert.Equal(newer.CharacterId, node.Connections.Members[1].CharacterId);
        Assert.Equal(ClientState.Connected, node.Connections.Members[1].State);
        handler.Handle(
            new ClientRemovedPackage
                { ClientId = 1, ConnectionEpoch = newer.ConnectionEpoch, ConnectionGeneration = 2 },
            node.CreateReceiveContext(authority)
        );
        Assert.Empty(node.Connections.Members);
        Assert.False(ClientPackageHandler.InstallController(node, newer));
        node.StopImmediate();
    }

    [Fact]
    public void BootstrapAllocatesItsLifetimeBeforeMetadataAndRetryKeepsIt()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            false,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty) { Node = node };
        Guid observedEpoch = default;
        long observedGeneration = 0;
        node.OnClientTransportConnected += connected =>
        {
            var metadata = ClientPackageBuilder.Capture(connected);
            Assert.NotEqual(Guid.Empty, metadata.ConnectionEpoch);
            Assert.True(metadata.ConnectionGeneration > 0);
            if (observedEpoch != Guid.Empty)
            {
                Assert.Equal(observedEpoch, metadata.ConnectionEpoch);
                Assert.Equal(observedGeneration, metadata.ConnectionGeneration);
            }

            observedEpoch = metadata.ConnectionEpoch;
            observedGeneration = metadata.ConnectionGeneration;
        };
        node.BeginClientBootstrap(client);
        node.BeginClientBootstrap(client);
        node.StopImmediate();
    }
}
