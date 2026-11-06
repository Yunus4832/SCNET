using System.Reflection;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class OutboundRuntimeLifetimeTest
{
    [Fact]
    public void ClosingAWorldImmediatelyPurgesQueuedLogicalWorkBeforeItCanRetargetTheNextWorld()
    {
        using var oldProject = new Project();
        using var nextProject = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var old = node.Runtime;
        old.BindProject(oldProject);
        NetworkSender.SendGlobal(oldProject, new SubsystemTimePackage(7, 0));
        Assert.Equal(1, node.PendingPackageCount);

        old.Close();
        Assert.Equal(0, node.PendingPackageCount);
        node.ReleaseProject(oldProject);
        node.PrepareWorldRuntime().BindProject(nextProject);
        node.QueuePackage(old, new SubsystemTimePackage(8, 0), PackageAudience.Global);
        Assert.Equal(0, node.PendingPackageCount);
        NetworkSender.SendGlobal(nextProject, new SubsystemTimePackage(9, 0));
        var pending = (PendingLogicalMessages)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node)!;
        Assert.Same(node.Runtime, Assert.Single(pending).Runtime);
        Assert.Equal(9, Assert.IsType<SubsystemTimePackage>(pending[0].Package).Time);
    }

    [Fact]
    public void DeferredNativeWorkIsPurgedAndRetiresItsFanoutExactlyOnce()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        node.Runtime.BindProject(project);
        var source = node.Runtime;
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty) { Connection = new ConnectedStub() };
        var fanout = new NetworkMessageFanout(
            node.SendStatistics,
            typeof(SubsystemTimePackage),
            NetworkChannel.Control
        );
        Assert.True(
            node.QueueNetworkPackage(
                client,
                new OutboundPackage(new SubsystemTimePackage(1, 0), PackageAudience.To(client), source)
                    { Fanout = fanout }
            )
        );

        node.ReleaseProject(project);

        var pending = (Dictionary<Client, PendingNetworkMessages>)typeof(NetNode)
            .GetField("_networkPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node)!;
        Assert.Empty(pending);
        var statistics = Assert.Single(node.SendStatistics.GetFanout());
        Assert.Equal(1, statistics.Messages);
        Assert.Equal(0, statistics.RecipientDeliveries);
        Assert.False(
            node.QueueNetworkPackage(
                client,
                new OutboundPackage(new SubsystemTimePackage(2, 0), PackageAudience.To(client), source)
            )
        );
        node.StopImmediate();
        Assert.Equal(statistics, Assert.Single(node.SendStatistics.GetFanout()));
    }

    [Fact]
    public void CoalescingAndWorkIdentityIncludeTheSourceLifetime()
    {
        var first = new WorldRuntimeContext(RuntimeSide.Authority, false);
        var second = new WorldRuntimeContext(RuntimeSide.Authority, false);
        try
        {
            var message = new OnlineCharacterStatePackage();
            var older = new OutboundPackage(message, PackageAudience.Global, first);
            var newer = new OutboundPackage(message, PackageAudience.Global, second);
            var pending = new List<OutboundPackage> { older };
            Assert.False(SnapshotPackageCoalescer.TryCoalesce(pending, newer));
            Assert.Equal(
                2,
                new HashSet<OutboundPackage>([older, newer], OutboundPackageIdentityComparer.Instance).Count
            );
        }
        finally
        {
            first.Close();
            second.Close();
        }
    }

    [Fact]
    public void InProcessConnectionRejectsWorkStampedForAnotherWorld()
    {
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        var pair = InProcessMessageConnection.CreatePair(
            authority,
            new Client(Guid.Empty),
            client,
            new Client(Guid.Empty)
        );
        using var toClient = pair.ToClient;
        using var toAuthority = pair.ToAuthority;
        Assert.Equal(
            ConnectionSendResult.Closed,
            toClient.TrySendOwned(
                new OutboundPackage(new SubsystemTimePackage(1, 0), PackageAudience.Global, client.Runtime)
            )
        );
        Assert.Equal(0, WorldNetworkBinding.For(client.Runtime).Inbox.Count);
        Assert.Equal(
            ConnectionSendResult.Accepted,
            toClient.TrySendOwned(
                new OutboundPackage(new SubsystemTimePackage(2, 0), PackageAudience.Global, authority.Runtime)
            )
        );
        Assert.Equal(1, WorldNetworkBinding.For(client.Runtime).Inbox.Count);
        authority.StopImmediate();
        client.StopImmediate();
    }

    private sealed class ConnectedStub : IMessageConnection
    {
        public bool IsConnected => true;

        public bool IsInProcess => false;

        public ConnectionSendResult TrySendOwned(OutboundPackage message) => ConnectionSendResult.Accepted;

        public void Disconnect(string reason)
        {
        }

        public void Dispose()
        {
        }
    }
}
