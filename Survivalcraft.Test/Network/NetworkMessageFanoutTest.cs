using System.Reflection;

using Game.Network;
using Game.Network.Packages;

namespace Survivalcraft.Test.Network;

public sealed class NetworkMessageFanoutTest
{
    [Fact]
    public void FlushingWithoutRecipientsCountsEachLogicalEmission()
    {
        var node = new NetNode();
        var package = new EntityPackage(42);
        node.QueuePackage(package, PackageAudience.Global);
        node.QueuePackage(package, PackageAudience.Global);
        var flush = typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(flush);
        flush.Invoke(node, null);
        var result = Assert.Single(node.SendStatistics.GetFanout());
        Assert.Equal(typeof(EntityPackage), result.PackageType);
        Assert.Equal(2, result.Messages);
        Assert.Equal(0, result.RecipientDeliveries);
        Assert.Equal(0d, result.AverageFanout);
        flush.Invoke(node, null);
        Assert.Equal(result, Assert.Single(node.SendStatistics.GetFanout()));
    }

    [Fact]
    public void ReplacingOneDeferredRecipientDoesNotCompleteOtherRecipients()
    {
        var statistics = new NetworkSendStatistics();
        var message = new NetworkMessageFanout(statistics, typeof(OnlinePlayerStatePackage), NetworkChannel.Bulk);
        var first = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null);
        var second = new Client(null, 2, Guid.NewGuid(), Guid.NewGuid(), null);
        message.Advance(1, 2);
        var pending = new List<OutboundPackage>
        {
            new(new OnlinePlayerStatePackage(), PackageAudience.To(first))
            {
                Fanout = message
            },
            new(new OnlinePlayerStatePackage(), PackageAudience.To(second))
            {
                Fanout = message
            }
        };
        Assert.True(SnapshotPackageCoalescer.TryCoalesce(pending,
            new OutboundPackage(new OnlinePlayerStatePackage(), PackageAudience.To(first))));
        Assert.Empty(statistics.GetFanout());
        Assert.Same(message, pending[1].Fanout);
        message.DeliveredTo(second);
        message.Advance(1, 0);
        Assert.Equal(1, Assert.Single(statistics.GetFanout()).RecipientDeliveries);
    }

    [Fact]
    public void ConcurrentContinuationsRecordOneMessageWithDistinctRecipients()
    {
        var statistics = new NetworkSendStatistics();
        var message = new NetworkMessageFanout(statistics, typeof(EntityPackage), NetworkChannel.Bulk);
        var clients = Enumerable.Range(0, 64)
            .Select(_ => new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null)).ToArray();
        message.Advance(1, clients.Length);
        Parallel.ForEach(clients, client =>
        {
            message.DeliveredTo(client);
            message.DeliveredTo(client);
            message.Advance(1, 0);
        });
        var result = Assert.Single(statistics.GetFanout());
        Assert.Equal(1, result.Messages);
        Assert.Equal(clients.Length, result.RecipientDeliveries);
        Assert.Equal(clients.Length, result.MaximumFanout);
    }

    [Fact]
    public void DeferredCopiesFinishOneMessageAndDeduplicateRecipientFragments()
    {
        var statistics = new NetworkSendStatistics();
        var message = new NetworkMessageFanout(statistics, typeof(EntityPackage), NetworkChannel.Bulk);
        var first = new Client(null, 7, Guid.NewGuid(), Guid.NewGuid(), null);
        var second = new Client(null, 7, Guid.NewGuid(), Guid.NewGuid(), null);
        message.DeliveredTo(first);
        message.DeliveredTo(first);
        message.Advance(1, 2);
        Assert.Empty(statistics.GetFanout());
        message.DeliveredTo(second);
        message.Advance(1, 0);
        Assert.Empty(statistics.GetFanout());
        message.DeliveredTo(first);
        message.Advance(1, 0);
        Assert.Equal(new NetworkPackageFanoutStatistics(typeof(EntityPackage), NetworkChannel.Bulk, 1, 2, 2),
            Assert.Single(statistics.GetFanout()));
        Assert.Throws<InvalidOperationException>(() => message.Advance(1, 0));
        Assert.Throws<InvalidOperationException>(() => message.DeliveredTo(first));
    }

    [Fact]
    public void CancelledContinuationsKeepOnlyActualRecipients()
    {
        var statistics = new NetworkSendStatistics();
        var message = new NetworkMessageFanout(statistics, typeof(EntityPackage), NetworkChannel.Bulk);
        message.Advance(1, 2);
        message.DeliveredTo(new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null));
        message.Advance(2, 0);
        Assert.Equal(1, Assert.Single(statistics.GetFanout()).RecipientDeliveries);
        var empty = new NetworkMessageFanout(statistics, typeof(EntityPackage), NetworkChannel.Bulk);
        empty.Advance(1, 0);
        var result = Assert.Single(statistics.GetFanout());
        Assert.Equal(2, result.Messages);
        Assert.Equal(0.5d, result.AverageFanout);
    }

    [Fact]
    public void InvalidContinuationCountsDoNotChangeMessage()
    {
        var statistics = new NetworkSendStatistics();
        var message = new NetworkMessageFanout(statistics, typeof(EntityPackage), NetworkChannel.Bulk);
        Assert.Throws<ArgumentOutOfRangeException>(() => message.Advance(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => message.Advance(1, -1));
        Assert.Throws<InvalidOperationException>(() => message.Advance(2, 0));
        message.Advance(1, 0);
        Assert.Equal(0, Assert.Single(statistics.GetFanout()).RecipientDeliveries);
    }

    [Fact]
    public void CoalescingEndsReplacedContinuationWithoutTransferringItsRecipients()
    {
        var statistics = new NetworkSendStatistics();
        var message = new NetworkMessageFanout(statistics, typeof(OnlinePlayerStatePackage), NetworkChannel.Bulk);
        message.DeliveredTo(new Client(null, 1, Guid.NewGuid(), Guid.NewGuid(), null));
        var pending = new List<OutboundPackage>
        {
            new(new OnlinePlayerStatePackage(), PackageAudience.Global)
            {
                Fanout = message
            }
        };
        Assert.True(SnapshotPackageCoalescer.TryCoalesce(pending,
            new OutboundPackage(new OnlinePlayerStatePackage(), PackageAudience.Global)));
        Assert.Null(Assert.Single(pending).Fanout);
        Assert.Equal(1, Assert.Single(statistics.GetFanout()).RecipientDeliveries);
    }
}
