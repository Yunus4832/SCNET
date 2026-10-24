using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class ServerConnectionIdentityTest
{
    [Fact]
    public void RepeatedServerMetadataPreservesQueuedAudienceIdentity()
    {
        var server = new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);
        var audience = PackageAudience.To(server);
        var incoming = new Client(null, 0, Guid.NewGuid(), server.CharacterId, null) { State = ClientState.Playing };

        var resolved = ClientPackageHandler.ResolveServerClient(server, incoming, server);

        Assert.Same(server, resolved);
        Assert.True(audience.Includes(resolved));
        Assert.Equal(incoming.TokenId, resolved.TokenId);
        Assert.Equal(ClientState.Playing, resolved.State);
    }

    [Fact]
    public void BootstrapBindsCharacterMetadataWithoutReplacingTheTransportSession()
    {
        var transport = new Client(null, 0, Guid.NewGuid(), Guid.Empty, null);
        var incoming = new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);

        var resolved = ClientPackageHandler.ResolveServerClient(transport, incoming, transport);

        Assert.Same(transport, resolved);
        Assert.Equal(incoming.CharacterId, resolved.CharacterId);
        Assert.True(PackageAudience.To(transport).Includes(resolved));
        Assert.False(PackageAudience.To(incoming).Includes(resolved));
    }
}
