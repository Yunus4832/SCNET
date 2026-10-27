using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class ClientPackageAuthorityTest
{
    [Fact]
    public void ClientCannotChangeAnotherSessionsState()
    {
        var node = new NetNode();
        var actor = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null) { State = ClientState.Connected };
        var other = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid(), null) { State = ClientState.Connected };
        node.Clients[actor.ID] = actor;
        node.Clients[other.ID] = other;
        var changes = 0;
        node.OnClientStateChanged += _ => changes++;
        var handler = new ClientPackageHandler();
        handler.Handle(
            new ClientStateChangedPackage { ClientId = other.ID, State = ClientState.Playing },
            new PackageReceiveContext(node, true, actor)
        );
        Assert.Equal(ClientState.Connected, other.State);
        Assert.Equal(0, changes);

        handler.Handle(
            new ClientStateChangedPackage { ClientId = actor.ID, State = ClientState.Playing },
            new PackageReceiveContext(node, true, actor)
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
        var node = new NetNode();
        var actor = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        node.Clients[actor.ID] = actor;
        ClientPackage package = type switch
        {
            ClientPackage.EventType.Add => new ClientAddedPackage
                { Client = new ClientIdentity(actor.ID, Guid.Empty, Guid.Empty, null) },
            ClientPackage.EventType.Remove => new ClientRemovedPackage { ClientId = actor.ID },
            ClientPackage.EventType.SyncList => new ClientListPackage(),
            _ => new ClientStateChangedPackage { ClientId = actor.ID, State = ClientState.NotConnected }
        };
        new ClientPackageHandler().Handle(package, new PackageReceiveContext(node, true, actor));
        Assert.Same(actor, Assert.Single(node.Clients).Value);
    }
}
