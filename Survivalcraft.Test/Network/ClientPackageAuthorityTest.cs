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
        handler.Handle(new ClientPackage(other.ID, ClientState.Playing), new PackageReceiveContext(node, true, actor));
        Assert.Equal(ClientState.Connected, other.State);
        Assert.Equal(0, changes);

        handler.Handle(new ClientPackage(actor.ID, ClientState.Playing), new PackageReceiveContext(node, true, actor));
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
        var package = new ClientPackage(actor.ID) with { PackageEventType = type };
        new ClientPackageHandler().Handle(package, new PackageReceiveContext(node, true, actor));
        Assert.Same(actor, Assert.Single(node.Clients).Value);
    }
}
