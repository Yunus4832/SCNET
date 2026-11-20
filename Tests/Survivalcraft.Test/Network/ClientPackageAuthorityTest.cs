using Game.Network;
using Game.Network.Handling;

namespace Survivalcraft.Test.Network;

public sealed class ClientPackageAuthorityTest
{
    [Fact]
    public void ClientCannotChangeAnotherSessionsState()
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var actor = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid()) { State = ClientState.Connected };
        var other = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid()) { State = ClientState.Connected };
        node.AddClient(actor);
        node.AddClient(other);
        var changes = 0;
        node.OnClientStateChanged += _ => changes++;
        var handler = new ClientPackageHandler();
        handler.Handle(
            ClientPackageBuilder.State(other) with { State = ClientState.Playing },
            node.CreateReceiveContext(actor)
        );
        Assert.Equal(ClientState.Connected, other.State);
        Assert.Equal(0, changes);

        handler.Handle(
            ClientPackageBuilder.State(actor) with { State = ClientState.Playing },
            node.CreateReceiveContext(actor)
        );
        Assert.Equal(ClientState.Playing, actor.State);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData(ClientPackage.EventType.Add)]
    [InlineData(ClientPackage.EventType.Remove)]
    [InlineData(ClientPackage.EventType.SyncList)]
    public void ClientCannotReplaceServerOwnedSessionDirectory(ClientPackage.EventType type)
    {
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var actor = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        node.AddClient(actor);
        ClientPackage package = type switch
        {
            ClientPackage.EventType.Add => new ClientAddedPackage
            {
                Client = new ClientIdentity(
                    actor.ID,
                    Guid.Empty,
                    Guid.Empty,
                    null,
                    ClientState.Connected,
                    Guid.NewGuid(),
                    1
                )
            },
            ClientPackage.EventType.Remove => new ClientRemovedPackage { ClientId = actor.ID },
            ClientPackage.EventType.SyncList => new ClientListPackage(),
            _ => new ClientStateChangedPackage { ClientId = actor.ID, State = ClientState.NotConnected }
        };
        new ClientPackageHandler().Handle(
            package,
            node.CreateReceiveContext(actor)
        );
        Assert.Same(actor, Assert.Single(node.Clients).Value);
    }
}
