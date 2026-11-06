using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Chunks;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Terrains.Distribution;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Network;

[Collection(ConfigFileCollection.Name)]
public sealed class NativeTerrainDeliveryTest : IDisposable
{
    private readonly Project _project = new();

    private readonly NetNode _node = new(
        NetNodeOptions.Default,
        true,
        PackageHandlerCatalog.Empty,
        NodeRole.Authority
    );

    private readonly Client _client;

    private readonly NativeTerrainDelivery _delivery;

    private ChunkDeliveryView? _view = new(Vector2.Zero, Vector2.Zero, 256);

    public NativeTerrainDeliveryTest()
    {
        _node.Runtime.BindProject(_project);
        _client = new Client(null, 1, Guid.NewGuid(), Guid.Empty) { Connection = new ConnectedStub() };
        _delivery = new NativeTerrainDelivery(_node, _ => _view, new NetNodeOptions(2, 100, 128 * 1024));
    }

    public void Dispose() => _project.Dispose();

    [Fact]
    public void FullRetriesKeepTheCapturedBaselineWhileAuthorityVersionsAdvance()
    {
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        SeedCache(new EncodedTerrainChunk(allocation.Coords, 1, new byte[1000]));
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(allocation, 1)));
        _delivery.Update(0);
        Assert.Single(QueuedFragments());
        SeedCache(new EncodedTerrainChunk(allocation.Coords, 2, new byte[1000]));
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(allocation, 2)));
        _delivery.Update(1);
        var baseline = QueuedFragments();
        Assert.Equal([0, 0, 1, 2], baseline.Select(fragment => (int)fragment.FragmentIndex));
        Assert.All(baseline, fragment => Assert.Equal(1, fragment.ContentVersion));
        Assert.Equal(0, _delivery.GetBacklog(_client).Pending);
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(allocation, 2, 1)));
        _delivery.Update(2);
        Assert.All(QueuedFragments().Skip(4), fragment => Assert.Equal(2, fragment.ContentVersion));
    }

    [Fact]
    public void RecoveryReplacesAnActiveBaselineAlreadyInstalledByTheClient()
    {
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        SeedCache(new EncodedTerrainChunk(allocation.Coords, 1, new byte[1000]));
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(allocation, 1)));
        _delivery.Update(0);
        Assert.Single(QueuedFragments());
        Assert.Equal(1, _delivery.GetBacklog(_client).Pending);
        SeedCache(new EncodedTerrainChunk(allocation.Coords, 2, new byte[1000]));

        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(allocation, 2, 1)));
        _delivery.Update(1);

        var replacement = QueuedFragments();
        Assert.NotEmpty(replacement);
        Assert.All(replacement, fragment => Assert.Equal(2, fragment.ContentVersion));
    }

    [Fact]
    public void EncodingUsesTheAcceptedVersionEvenWhileNewerVersionsArrive()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var delivery = new NativeTerrainDelivery(
            _node,
            _ => _view,
            new NetNodeOptions(1, 100, 128 * 1024),
            (snapshot, _) =>
            {
                started.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5))) { throw new TimeoutException(); }

                return NetworkChunkCodec.Encode(snapshot);
            }
        );
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        try
        {
            Assert.Equal(ConnectionSendResult.Accepted, delivery.TryAccept(_client, Message(allocation, 1)));
            delivery.Update(0);
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            for (var version = 2; version <= 5; version++)
            {
                Assert.Equal(ConnectionSendResult.Accepted, delivery.TryAccept(_client, Message(allocation, version)));
            }

            release.Set();
            var now = 0d;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        delivery.Update(now += 0.01);
                        return delivery.GetBacklog(_client).Pending == 0;
                    },
                    TimeSpan.FromSeconds(5)
                )
            );
            Assert.NotEmpty(QueuedFragments());
            Assert.All(QueuedFragments(), fragment => Assert.Equal(1, fragment.ContentVersion));
        }
        finally { release.Set(); }
    }

    [Fact]
    public void NearSnapshotsPreemptFarTransfersWithinCountAndPinnedByteBounds()
    {
        foreach (var x in Enumerable.Range(1, 4))
        {
            var allocation = new ChunkAllocationId(new Point2(x, 0), 1);
            SeedCache(new EncodedTerrainChunk(allocation.Coords, 1, new byte[2 * 1024 * 1024]));
            Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(allocation, 1)));
        }

        _delivery.Update(0);
        Assert.Equal(
            ConnectionSendResult.Backpressured,
            _delivery.TryAccept(
                _client,
                Message(new ChunkAllocationId(new Point2(5, 0), 1), 1)
            )
        );
        var near = new ChunkAllocationId(new Point2(0, 0), 1);
        SeedCache(new EncodedTerrainChunk(near.Coords, 1, new byte[2 * 1024 * 1024]));
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(near, 1)));
        _delivery.Update(1);
        Assert.Contains(QueuedFragments(), fragment => fragment.Allocation == near);
        var backlog = _delivery.GetBacklog(_client);
        Assert.InRange(backlog.Pending, 1, NativeTerrainDelivery.maximumActiveTransfersPerClient);
        Assert.InRange(backlog.PinnedBytes, 1, NativeTerrainDelivery.maximumActivePayloadBytesPerClient);
        _view = new ChunkDeliveryView(new Vector2(1000), new Vector2(1000), 64);
        _delivery.Trim(_client);
        Assert.Equal((0, 0), _delivery.GetBacklog(_client));
        Assert.Empty(QueuedFragments());
    }

    [Fact]
    public void ReplacementAllocationsCancelQueuedFragmentsAndIgnoreOlderRepairs()
    {
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        SeedCache(new EncodedTerrainChunk(allocation.Coords, 1, new byte[10000]));
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(allocation, 1)));
        _delivery.Update(0);
        Assert.NotEmpty(QueuedFragments());
        var replacement = allocation with { Generation = 2 };
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(replacement, 1)));
        Assert.Empty(QueuedFragments());
        Assert.True(_delivery.RequestMissing(_client, new TerrainChunkFragmentRequest(allocation, 1, 29, [0])));
        _delivery.Update(1);
        Assert.All(QueuedFragments(), fragment => Assert.Equal(replacement, fragment.Allocation));
    }

    [Fact]
    public void RepairWithoutCapacityPreservesTheBaselineAndHigherPriorityTransfers()
    {
        var far = new ChunkAllocationId(new Point2(4, 0), 1);
        var near = new ChunkAllocationId(new Point2(0, 0), 1);
        var middle = new ChunkAllocationId(new Point2(1, 0), 1);
        SeedCache(new EncodedTerrainChunk(far.Coords, 1, new byte[1000]));
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(far, 1)));
        _delivery.Update(0);
        SeedCache(new EncodedTerrainChunk(near.Coords, 1, new byte[2 * 1024 * 1024]));
        SeedCache(new EncodedTerrainChunk(middle.Coords, 1, new byte[2 * 1024 * 1024 - 1000]));
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(near, 1)));
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(middle, 1)));
        _delivery.Update(0);
        var backlog = _delivery.GetBacklog(_client);
        Assert.Equal(3, backlog.Pending);
        Assert.Equal(4 * 1024 * 1024, backlog.PinnedBytes);
        var replacement = new EncodedTerrainChunk(far.Coords, 2, new byte[2 * 1024 * 1024]);
        SeedCache(replacement);
        var fragments = (ushort)new ChunkTransmission(replacement, far).FragmentCount;

        Assert.False(_delivery.RequestMissing(_client, new TerrainChunkFragmentRequest(far, 2, fragments, [0])));

        Assert.Equal(backlog, _delivery.GetBacklog(_client));
        Assert.Equal(far, Assert.Single(QueuedFragments()).Allocation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedRepairReplacementPreservesTheActiveBaseline(bool cachedReplacement)
    {
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        SeedCache(new EncodedTerrainChunk(allocation.Coords, 1, new byte[1000]));
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(allocation, 1)));
        _delivery.Update(0);
        if (cachedReplacement)
        {
            SeedCache(new EncodedTerrainChunk(allocation.Coords, 2, new byte[1000]));
        }

        Assert.False(_delivery.RequestMissing(_client, new TerrainChunkFragmentRequest(allocation, 2, 3, [3])));
        Assert.Equal(1, _delivery.GetBacklog(_client).Pending);
        _delivery.Update(1);
        Assert.Equal(3, QueuedFragments().Length);
        Assert.All(QueuedFragments(), fragment => Assert.Equal(1, fragment.ContentVersion));
        Assert.Equal(0, _delivery.GetBacklog(_client).Pending);
    }

    [Fact]
    public void CachedRepairsSendOnlyRequestedFragmentsAndCloseRetiresHeldFanouts()
    {
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        var encoded = new EncodedTerrainChunk(allocation.Coords, 1, new byte[1000]);
        SeedCache(encoded);
        Assert.True(_delivery.RequestMissing(_client, new TerrainChunkFragmentRequest(allocation, 1, 3, [1])));
        _delivery.Update(0);
        Assert.Equal(1, Assert.Single(QueuedFragments()).FragmentIndex);
        Assert.Equal(1, _delivery.FragmentsRetransmitted);
        Assert.False(_delivery.RequestMissing(_client, new TerrainChunkFragmentRequest(allocation, 2, 3, [1])));
        var next = allocation with { Generation = 2 };
        Assert.Equal(ConnectionSendResult.Accepted, _delivery.TryAccept(_client, Message(next, 1, fanout: true)));
        _delivery.Update(1);
        _node.Runtime.Close();
        Assert.Equal((0, 0), _delivery.GetBacklog(_client));
        Assert.Empty(QueuedFragments());
        var statistics = _node.SendStatistics.GetFanout()
            .Single(value => value.PackageType == typeof(TerrainSnapshotMessage));
        Assert.Equal(1, statistics.Messages);
        Assert.Equal(0, statistics.RecipientDeliveries);
        Assert.Equal(ConnectionSendResult.Closed, _delivery.TryAccept(_client, Message(next, 1)));
    }

    [Fact]
    public void ChunkRemovalRejectsAnInflightEncodingBeforeTheCoordinateIsReused()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var delivery = new NativeTerrainDelivery(
            _node,
            _ => _view,
            new NetNodeOptions(1, 100, 128 * 1024),
            (snapshot, _) =>
            {
                started.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5))) { throw new TimeoutException(); }

                return NetworkChunkCodec.Encode(snapshot);
            }
        );
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        try
        {
            Assert.Equal(ConnectionSendResult.Accepted, delivery.TryAccept(_client, Message(allocation, 1)));
            delivery.Update(0);
            Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
            delivery.RemoveChunk(allocation.Coords);
            var replacement = allocation with { Generation = 2 };
            Assert.Equal(
                ConnectionSendResult.Accepted,
                delivery.TryAccept(
                    _client,
                    Message(replacement, 1, firstCell: 7)
                )
            );
            release.Set();
            var now = 0d;
            Assert.True(
                SpinWait.SpinUntil(
                    () =>
                    {
                        delivery.Update(now += 0.01);
                        return delivery.GetBacklog(_client).Pending == 0;
                    },
                    TimeSpan.FromSeconds(5)
                )
            );
            var fragment = Assert.Single(QueuedFragments());
            Assert.Equal(replacement, fragment.Allocation);
            var decoded = NetworkChunkCodec.DecodeSnapshot(replacement, 1, fragment.Payload);
            Assert.Equal(7, decoded.Cells.Span[0]);
        }
        finally { release.Set(); }
    }

    private OutboundPackage Message(
        ChunkAllocationId allocation,
        long version,
        long known = 0,
        bool fanout = false,
        int firstCell = 0
    )
    {
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[0] = firstCell;
        var snapshot = new ClientChunkSnapshot(
            allocation,
            version,
            cells,
            new long[AuthorityChunkSnapshot.ShaftCount]
        );
        return new OutboundPackage(
            new TerrainSnapshotMessage(snapshot, known),
            PackageAudience.To(_client),
            _node.Runtime
        )
        {
            Fanout = fanout
                ? new NetworkMessageFanout(
                    _node.SendStatistics,
                    typeof(TerrainSnapshotMessage),
                    PackageTransportPolicy.Control.Channel
                )
                : null
        };
    }

    [Fact]
    public void ExplicitChunkRateControlsDeliveryWithoutAmbientSettingsReads()
    {
        using var delivery = new NativeTerrainDelivery(_node, _ => _view, new NetNodeOptions(2, 1, 1024 * 1024));
        var cache = (NetworkChunkCache)typeof(NativeTerrainDelivery)
            .GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(delivery)!;
        foreach (var x in new[] { 0, 1 })
        {
            var allocation = new ChunkAllocationId(new Point2(x, 0), 1);
            cache.Store(new EncodedTerrainChunk(allocation.Coords, 1, new byte[1000]));
            Assert.Equal(ConnectionSendResult.Accepted, delivery.TryAccept(_client, Message(allocation, 1)));
        }

        delivery.Update(0);
        Assert.Single(QueuedFragments().Select(fragment => fragment.Allocation).Distinct());
        delivery.Update(.5);
        Assert.Single(QueuedFragments().Select(fragment => fragment.Allocation).Distinct());
        delivery.Update(1);
        Assert.Equal(2, QueuedFragments().Select(fragment => fragment.Allocation).Distinct().Count());
    }

    private void SeedCache(EncodedTerrainChunk chunk)
    {
        var cache = (NetworkChunkCache)typeof(NativeTerrainDelivery)
            .GetField("_cache", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_delivery)!;
        cache.Store(chunk);
    }

    private EncodedTerrainChunkFragment[] QueuedFragments()
    {
        var pending = (Dictionary<Client, PendingNetworkMessages>)typeof(NetNode)
            .GetField("_networkPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_node)!;
        return pending.Values.SelectMany(value => value).Select(value => value.Package).OfType<TerrainFragmentPackage>()
            .Select(value => value.ChunkFragment).ToArray();
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
