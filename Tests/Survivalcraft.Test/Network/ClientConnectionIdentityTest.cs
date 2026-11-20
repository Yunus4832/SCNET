using Game.Network;

namespace Survivalcraft.Test.Network;

public sealed class ClientConnectionIdentityTest
{
    [Fact]
    public void ReusedShortIdsAndCharactersDoNotMakeDifferentConnectionsEqual()
    {
        var character = Guid.NewGuid();
        var first = new Client(null, 1, Guid.NewGuid(), character);
        var second = new Client(null, 1, Guid.NewGuid(), character);
        Assert.NotSame(first, second);
        Assert.NotEqual(first, second);
        Assert.False(first == second);
        Assert.Equal(2, new HashSet<Client> { first, second }.Count);
    }

    [Fact]
    public void ConnectionHashRemainsStableWhenPublicDirectoryOrCharacterChanges()
    {
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
        var dictionary = new Dictionary<Client, string> { [client] = "connection" };
        client.AssignRoutingId(2);
        client.SetControlledCharacter(Guid.NewGuid());
        Assert.Equal("connection", dictionary[client]);
    }
}
