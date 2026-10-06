using System.Diagnostics;

using Game.Network;
using Game.Network.Packages;

namespace Survivalcraft.Test.Network;

public sealed class NetworkSendStatisticsTest
{
    [Fact]
    public void FanoutIncludesZeroRecipientMessagesAndKeepsImmutableSnapshots()
    {
        var statistics = new NetworkSendStatistics();
        Assert.Empty(statistics.GetFanout());
        statistics.RecordFanout(typeof(EntityPackage), NetworkChannel.Bulk, 0);
        var first = Assert.Single(statistics.GetFanout());
        statistics.RecordFanout(typeof(EntityPackage), NetworkChannel.Bulk, 2);
        statistics.RecordFanout(typeof(EntityPackage), NetworkChannel.Bulk, 4);

        Assert.Equal(new NetworkPackageFanoutStatistics(typeof(EntityPackage), NetworkChannel.Bulk, 1, 0, 0),
            first);
        Assert.Equal(0d, first.AverageFanout);
        var latest = Assert.Single(statistics.GetFanout());
        Assert.Equal(new NetworkPackageFanoutStatistics(typeof(EntityPackage), NetworkChannel.Bulk, 3, 6, 4),
            latest);
        Assert.Equal(2d, latest.AverageFanout);
        Assert.Empty(statistics.GetPackages());
        Assert.Empty(statistics.GetChannels());
    }

    [Fact]
    public void FanoutSeparatesPackageTypesAndChannelsAndRejectsInvalidSamples()
    {
        var statistics = new NetworkSendStatistics();
        statistics.RecordFanout(typeof(EntityPackage), NetworkChannel.Bulk, 3);
        statistics.RecordFanout(typeof(EntityPackage), NetworkChannel.Control, 1);
        statistics.RecordFanout(typeof(SubsystemBodyPackage), NetworkChannel.Bulk, 2);
        var snapshot = statistics.GetFanout();
        Assert.Equal(3, snapshot.Count);
        Assert.Contains(new NetworkPackageFanoutStatistics(typeof(EntityPackage), NetworkChannel.Bulk, 1, 3, 3),
            snapshot);
        Assert.Contains(new NetworkPackageFanoutStatistics(typeof(EntityPackage), NetworkChannel.Control, 1, 1, 1),
            snapshot);
        Assert.Contains(new NetworkPackageFanoutStatistics(typeof(SubsystemBodyPackage), NetworkChannel.Bulk, 1, 2, 2),
            snapshot);
        Assert.Throws<ArgumentNullException>(() => statistics.RecordFanout(null!, NetworkChannel.Bulk, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            statistics.RecordFanout(typeof(EntityPackage), NetworkChannel.Bulk, -1));
        Assert.Equal(snapshot, statistics.GetFanout());
    }

    [Fact]
    public void EncodingAttemptsAreIndependentOfDeliveries()
    {
        var statistics = new NetworkSendStatistics();
        statistics.RecordEncoding(3, 100, 50, Stopwatch.Frequency, Stopwatch.Frequency * 2);
        var first = statistics.GetEncoding();
        statistics.RecordEncoding(1, 20, 25, 0, 0);

        Assert.Equal(new NetworkEncodingStatistics(1, 3, 100, 50, 1000, 2000), first);
        Assert.Equal(new NetworkEncodingStatistics(2, 4, 120, 75, 1000, 2000), statistics.GetEncoding());
        Assert.Empty(statistics.GetPackages());
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.RecordEncoding(-1, 0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.RecordEncoding(0, -1, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.RecordEncoding(0, 0, -1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.RecordEncoding(0, 0, 0, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.RecordEncoding(0, 0, 0, 0, -1));
        Assert.Equal(new NetworkEncodingStatistics(2, 4, 120, 75, 1000, 2000), statistics.GetEncoding());
    }

    [Fact]
    public void RoutingTotalsKeepMaximumBatchAndSnapshotIsolation()
    {
        var statistics = new NetworkSendStatistics();
        statistics.RecordRouting(20, 3, Stopwatch.Frequency);
        var first = statistics.GetRouting();
        statistics.RecordRouting(10, 0, Stopwatch.Frequency * 2);

        Assert.Equal(new NetworkRoutingStatistics(20, 3, 1000, 1000), first);
        Assert.Equal(new NetworkRoutingStatistics(30, 3, 3000, 2000), statistics.GetRouting());
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.RecordRouting(-1, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.RecordRouting(1, -1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => statistics.RecordRouting(1, 0, -1));
        Assert.Equal(new NetworkRoutingStatistics(30, 3, 3000, 2000), statistics.GetRouting());
    }

    [Fact]
    public void CountsActualRecipientDeliveriesAndChannelBytes()
    {
        var statistics = new NetworkSendStatistics();
        statistics.Record(NetworkChannel.Bulk, 100, [(typeof(EntityPackage), 70), (typeof(EntityPackage), 40)]);
        statistics.Record(NetworkChannel.Bulk, 80, [(typeof(EntityPackage), 100)]);
        statistics.Record(NetworkChannel.StateStream, 20, [(typeof(SubsystemBodyPackage), 15)]);

        var channels = statistics.GetChannels();
        Assert.Equal(new NetworkChannelSendStatistics(2, 180), channels[NetworkChannel.Bulk]);
        Assert.Equal(new NetworkChannelSendStatistics(1, 20), channels[NetworkChannel.StateStream]);
        Assert.Contains(new NetworkPackageSendStatistics(typeof(EntityPackage), NetworkChannel.Bulk, 3, 210),
            statistics.GetPackages());
    }

    [Fact]
    public void SnapshotsDoNotChangeAfterFurtherSends()
    {
        var statistics = new NetworkSendStatistics();
        statistics.Record(NetworkChannel.Bulk, 100, [(typeof(EntityPackage), 80)]);
        var channels = statistics.GetChannels();
        var packages = statistics.GetPackages();

        statistics.Record(NetworkChannel.Bulk, 100, [(typeof(EntityPackage), 80)]);

        Assert.Equal(new NetworkChannelSendStatistics(1, 100), channels[NetworkChannel.Bulk]);
        Assert.Equal(1, Assert.Single(packages).Deliveries);
    }
}
