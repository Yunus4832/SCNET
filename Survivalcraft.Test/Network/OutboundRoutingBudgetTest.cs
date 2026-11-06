using System.Reflection;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Network;

public sealed class OutboundRoutingBudgetTest
{
    [Fact]
    public void TerrainBackpressureCannotBlockControlMessagesForAHealthyRecipient()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority,
            new ManualTimeProvider()
        );
        node.Runtime.BindProject(project);
        var connection = new SnapshotBlockingConnection();
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty)
        {
            Connection = connection, State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        node.AddClient(client);
        node.QueuePackage(
            new TerrainSnapshotMessage(
                new ClientChunkSnapshot(
                    new ChunkAllocationId(new(0, 0), 1),
                    1,
                    new int[AuthorityChunkSnapshot.CellCount],
                    new long[AuthorityChunkSnapshot.ShaftCount]
                )
            ),
            PackageAudience.To(client)
        );
        node.QueuePackage(new SubsystemTimePackage(9, 0), PackageAudience.To(client));

        typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(node, null);

        Assert.Equal(9d, Assert.Single(connection.Delivered));
        Assert.Equal(1, node.PendingPackageCount);
        node.Runtime.Close();
    }

    [Fact]
    public void RoutingStopsAfterItsTimeSliceAndRetainsUnfinishedFanoutForLaterUpdates()
    {
        using var project = new Project();
        var clock = new ManualTimeProvider();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Authority,
            clock
        );
        node.Runtime.BindProject(project);
        var connection = new AdvancingConnection(clock);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty)
        {
            Connection = connection, State = ClientState.Playing, ConnectionPhase = ConnectionPhase.Live
        };
        node.AddClient(client);
        for (var value = 1; value <= 3; value++)
        {
            node.QueuePackage(new SubsystemTimePackage(value, 0), PackageAudience.To(client));
        }

        var flush = typeof(NetNode).GetMethod("FlushPendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var turn = 1; turn <= 3; turn++)
        {
            flush.Invoke(node, null);
            Assert.Equal(3 - turn, node.PendingPackageCount);
            Assert.Equal(turn, connection.Delivered.Count);
            Assert.Equal(turn, Assert.Single(node.SendStatistics.GetFanout()).Messages);
        }

        Assert.Equal(new[] { 1d, 2d, 3d }, connection.Delivered);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        internal void Advance() => _timestamp += TimeSpan.FromMilliseconds(5).Ticks;
    }

    private sealed class SnapshotBlockingConnection : IMessageConnection
    {
        public List<double> Delivered { get; } = [];

        public bool IsConnected => true;

        public bool IsInProcess => true;

        public ConnectionSendResult TrySendOwned(OutboundPackage message)
        {
            if (message.Package is TerrainSnapshotMessage) { return ConnectionSendResult.Backpressured; }

            Delivered.Add(Assert.IsType<SubsystemTimePackage>(message.Package).Time);
            return ConnectionSendResult.Accepted;
        }

        public void Disconnect(string reason)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class AdvancingConnection(ManualTimeProvider clock) : IMessageConnection
    {
        public List<double> Delivered { get; } = [];

        public bool IsConnected => true;

        public bool IsInProcess => true;

        public ConnectionSendResult TrySendOwned(OutboundPackage message)
        {
            Delivered.Add(((SubsystemTimePackage)message.Package).Time);
            clock.Advance();
            return ConnectionSendResult.Accepted;
        }

        public void Disconnect(string reason)
        {
        }

        public void Dispose()
        {
        }
    }
}
