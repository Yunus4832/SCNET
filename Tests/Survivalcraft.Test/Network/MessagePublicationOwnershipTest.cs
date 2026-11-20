using System.Reflection;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class MessagePublicationOwnershipTest
{
    [Fact]
    public void PublicationAndInProcessDeliveryEachSeparateThePayloadOwner()
    {
        using var project = new Project();
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        authority.Runtime.BindProject(project);
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var player = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
        var server = new Client(null, 0, Guid.NewGuid(), Guid.Empty);
        var pair = InProcessMessageConnection.CreatePair(authority, player, client, server);
        using var toClient = pair.ToClient;
        using var toAuthority = pair.ToAuthority;
        var bytes = new byte[] { 1, 2, 3 };
        authority.QueuePackage(new ModEnvelopePackage("example.mod", "update", bytes), PackageAudience.To(player));
        var pending = (PendingLogicalMessages)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(authority)!;
        var published = Assert.Single(pending);
        bytes[0] = 99;
        Assert.Equal(1, Assert.IsType<ModEnvelopePackage>(published.Package).Payload[0]);
        Assert.Equal(ConnectionSendResult.Accepted, toClient.TrySendOwned(published));
        WorldNetworkBinding.For(client.Runtime).Inbox.Drain(
            1,
            received => Assert.IsType<ModEnvelopePackage>(received.Package).Payload[1] = 88
        );
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<ModEnvelopePackage>(published.Package).Payload);
        client.Runtime.Close();
    }
}
