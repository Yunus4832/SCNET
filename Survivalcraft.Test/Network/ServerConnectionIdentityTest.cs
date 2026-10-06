using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class ServerConnectionIdentityTest
{
    [Fact]
    public void RepeatedServerMetadataPreservesQueuedAudienceIdentity()
    {
        var server = new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);
        var audience = PackageAudience.To(server);
        var incoming = new Client(null, 0, Guid.NewGuid(), server.GUID, null) { State = ClientState.Playing };

        var resolved = ClientPackageHandler.ResolveServerClient(server, incoming, server);

        Assert.Same(server, resolved);
        Assert.True(audience.Includes(resolved));
        Assert.Equal(incoming.TokenId, resolved.TokenId);
        Assert.Equal(ClientState.Playing, resolved.State);
    }

    [Fact]
    public void BootstrapReplacesUnidentifiedTransportWithAuthoritativeServerIdentity()
    {
        var transport = new Client(null, 0, Guid.NewGuid(), Guid.Empty, null);
        var incoming = new Client(null, 0, Guid.NewGuid(), Guid.NewGuid(), null);

        var resolved = ClientPackageHandler.ResolveServerClient(transport, incoming, transport);

        Assert.Same(incoming, resolved);
        Assert.True(PackageAudience.To(resolved).Includes(incoming));
        Assert.False(PackageAudience.To(transport).Includes(resolved));
    }
}
