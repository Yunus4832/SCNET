using Game.Network;

namespace Survivalcraft.Test.Network;

public sealed class ClientSessionIdentityTest
{
    [Fact]
    public void ReusedShortIdsAndCharactersDoNotMakeDifferentSessionsEqual()
    {
        var character = Guid.NewGuid();
        var first = new Client(null, 1, Guid.NewGuid(), character, null);
        var second = new Client(null, 1, Guid.NewGuid(), character, null);
        Assert.NotEqual(first.SessionId, second.SessionId);
        Assert.NotEqual(first, second);
        Assert.False(first == second);
        Assert.Equal(2, new HashSet<Client> { first, second }.Count);
    }

    [Fact]
    public void SessionHashRemainsStableWhenPublicDirectoryOrCharacterChanges()
    {
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty, null);
        var dictionary = new Dictionary<Client, string> { [client] = "session" };
        client.ID = 2;
        client.CharacterId = Guid.NewGuid();
        Assert.Equal("session", dictionary[client]);
    }
}
