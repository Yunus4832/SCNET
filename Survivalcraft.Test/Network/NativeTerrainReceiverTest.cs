using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Chunks;
using Game.Network.Handling;
using Game.Network.Transport;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Network;

public sealed class NativeTerrainReceiverTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InboxSnapshotsReserveTheSameBudgetAndCancellationReleasesTheirCapacity(bool closeWorld)
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var content = new ClientChunkContentTransport(node.Runtime);
        using var receiver = new NativeTerrainReceiver(node.Runtime, content);
        using var cancelled = new CancellationTokenSource();
        var capacity = (int)(ClientChunkContentTransport.MaximumQueuedSnapshotBytes /
                             ClientChunkContentTransport.SnapshotBytes);
        var allocations = Enumerable.Range(0, capacity + 1)
            .Select(x => new ChunkAllocationId(new Point2(x, 0), 1)).ToArray();
        content.Request(allocations.Select(allocation => new ChunkContentRequest(allocation)).ToArray());
        foreach (var allocation in allocations.Take(capacity))
        {
            var message = receiver.Receive(Fragment(allocation)) ?? throw new InvalidOperationException("No snapshot.");
            Assert.True(
                WorldNetworkBinding.For(node.Runtime).Inbox.TryPostOwned(
                    new ReceivedPackage(message, node.CreateReceiveContext()),
                    cancelled.Token
                )
            );
        }

        Assert.Equal(0, content.PendingSnapshotBytes);
        Assert.Equal(capacity * ClientChunkContentTransport.SnapshotBytes, content.BufferedSnapshotBytes);
        Assert.Null(receiver.Receive(Fragment(allocations[^1])));
        if (closeWorld) { node.Runtime.Close(); }
        else
        {
            cancelled.Cancel();
            while (WorldNetworkBinding.For(node.Runtime).Inbox.Count > 0)
            {
                Assert.Equal(
                    0,
                    WorldNetworkBinding.For(node.Runtime).Inbox.Drain(
                        capacity + 1,
                        _ => throw new InvalidOperationException("Cancelled delivery.")
                    )
                );
            }
        }

        Assert.Equal(0, content.BufferedSnapshotBytes);
        Assert.Equal(0, WorldNetworkBinding.For(node.Runtime).Inbox.Count);
        using var retry = receiver.Receive(Fragment(allocations[0]));
        if (closeWorld) { Assert.Null(retry); }
        else
        {
            Assert.NotNull(retry);
            content.Receive(retry.Snapshot, retry.Reservation);
            Assert.Equal(ClientChunkContentTransport.SnapshotBytes, content.PendingSnapshotBytes);
            Assert.Equal(content.PendingSnapshotBytes, content.BufferedSnapshotBytes);
        }
    }

    [Fact]
    public void DeliveryTransfersReservedCapacityToContentWithoutDoubleAccounting()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var content = new ClientChunkContentTransport(node.Runtime);
        using var receiver = new NativeTerrainReceiver(node.Runtime, content);
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        content.Request([new ChunkContentRequest(allocation)]);
        var message = receiver.Receive(Fragment(allocation)) ?? throw new InvalidOperationException("No snapshot.");
        Assert.True(
            WorldNetworkBinding.For(node.Runtime).Inbox
                .TryPostOwned(new ReceivedPackage(message, node.CreateReceiveContext()))
        );
        Assert.Equal(ClientChunkContentTransport.SnapshotBytes, content.BufferedSnapshotBytes);
        Assert.Equal(
            1,
            WorldNetworkBinding.For(node.Runtime).Inbox.Drain(
                1,
                received =>
                {
                    var snapshot = Assert.IsType<TerrainSnapshotMessage>(received.Package);
                    content.Receive(snapshot.Snapshot, snapshot.Reservation);
                }
            )
        );
        Assert.Equal(ClientChunkContentTransport.SnapshotBytes, content.BufferedSnapshotBytes);
        Assert.Equal(content.PendingSnapshotBytes, content.BufferedSnapshotBytes);
        Assert.Equal(1, content.DrainReceived(new List<ClientChunkSnapshot>()));
        Assert.Equal(0, content.BufferedSnapshotBytes);
    }

    [Fact]
    public void AllocationCancellationTrimsLogicalAndAdaptedRequestsAndRetiresOneFanout()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        var server = new Client(Guid.Empty) { Connection = new ConnectedStub() };
        node.Server = server;
        var content = new ClientChunkContentTransport(node.Runtime);
        using var receiver = new NativeTerrainReceiver(node.Runtime, content);
        var partial = new ChunkAllocationId(new Point2(0, 0), 1);
        var untouched = new ChunkAllocationId(new Point2(1, 0), 1);
        var requests = new[] { new ChunkContentRequest(partial), new ChunkContentRequest(untouched) };
        content.Request(requests);
        var fragments = EncodedTerrainChunkFragmenter.Split(
            new EncodedTerrainChunk(partial.Coords, 1, new byte[1000]),
            partial
        ).ToArray();
        Assert.Null(receiver.Receive(fragments[0]));
        var wire = receiver.AdaptRequests(new TerrainChunksRequestPackage { ChunkRequests = requests });
        Assert.Equal(2, wire.Count);
        Assert.Equal(
            untouched,
            Assert.Single(Assert.IsType<TerrainChunksRequestPackage>(wire[0]).ChunkRequests).Allocation
        );
        var repair = Assert.Single(Assert.IsType<TerrainFragmentsRequestPackage>(wire[1]).FragmentRequests);
        Assert.Equal(partial, repair.Allocation);
        Assert.Equal([1, 2], repair.MissingFragmentIndices.Select(index => (int)index));
        Assert.Equal(1, receiver.FullChunkRequestsSent);
        Assert.Equal(1, receiver.MissingFragmentRequestsSent);
        node.QueueAdaptedNetworkPackages(
            server,
            new OutboundPackage(
                new TerrainChunksRequestPackage { ChunkRequests = requests },
                PackageAudience.To(server),
                node.Runtime
            ),
            wire
        );
        content.Discard(partial);
        Assert.Equal(0, receiver.PendingTransferCount);
        Assert.Empty(node.SendStatistics.GetFanout());
        content.Discard(untouched);
        Assert.Equal(0, node.PendingPackageCount);
        var statistics = Assert.Single(node.SendStatistics.GetFanout());
        Assert.Equal(1, statistics.Messages);
        Assert.Equal(0, statistics.RecipientDeliveries);
    }

    [Fact]
    public void LinkCancellationClearsReassemblyAndRejectsFurtherWork()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var content = new ClientChunkContentTransport(node.Runtime);
        using var closed = new CancellationTokenSource();
        using var receiver = new NativeTerrainReceiver(node.Runtime, content, closed.Token);
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        var request = new TerrainChunksRequestPackage { ChunkRequests = [new ChunkContentRequest(allocation)] };
        content.Request(request.ChunkRequests);
        var fragment = EncodedTerrainChunkFragmenter.Split(
            new EncodedTerrainChunk(allocation.Coords, 1, new byte[1000]),
            allocation
        ).First();
        Assert.Null(receiver.Receive(fragment));
        Assert.True(receiver.PendingTransferBytes > 0);
        closed.Cancel();
        Assert.Equal(0, receiver.PendingTransferBytes);
        Assert.Null(receiver.Receive(fragment));
        Assert.Empty(receiver.AdaptRequests(request));
    }

    private static EncodedTerrainChunkFragment Fragment(ChunkAllocationId allocation, long version = 1)
    {
        var encoded = NetworkChunkCodec.Encode(
            new AuthorityChunkSnapshot(
                allocation.Coords,
                version,
                new int[AuthorityChunkSnapshot.CellCount],
                new long[AuthorityChunkSnapshot.ShaftCount]
            )
        );
        return Assert.Single(EncodedTerrainChunkFragmenter.Split(encoded, allocation));
    }

    [Fact]
    public void NativeCompletionsCoalesceToTheSameNewestVersionAsLocalSnapshots()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var content = new ClientChunkContentTransport(node.Runtime);
        using var receiver = new NativeTerrainReceiver(node.Runtime, content);
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        content.Request([new ChunkContentRequest(allocation)]);
        Receive(receiver, content, Fragment(allocation));
        Receive(receiver, content, Fragment(allocation, 2));
        Assert.Null(receiver.Receive(Fragment(allocation)));
        Assert.Equal(ClientChunkContentTransport.SnapshotBytes, content.BufferedSnapshotBytes);
        var snapshots = new List<ClientChunkSnapshot>();
        Assert.Equal(1, content.DrainReceived(snapshots));
        Assert.Equal(2, snapshots[0].ContentVersion);
    }

    [Fact]
    public void FailedInboxDeliveryReleasesTheSlotAndMakesTheSameVersionRetryable()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var content = new ClientChunkContentTransport(node.Runtime);
        using var receiver = new NativeTerrainReceiver(node.Runtime, content);
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        content.Request([new ChunkContentRequest(allocation)]);
        var fragment = Fragment(allocation);
        var message = receiver.Receive(fragment) ?? throw new InvalidOperationException("No snapshot.");
        Assert.True(
            WorldNetworkBinding.For(node.Runtime).Inbox
                .TryPostOwned(new ReceivedPackage(message, node.CreateReceiveContext()))
        );
        Assert.Throws<InvalidOperationException>(() => WorldNetworkBinding.For(node.Runtime).Inbox.Drain(
                1,
                _ => throw new InvalidOperationException("Fixture delivery failure.")
            )
        );
        Assert.Equal(0, content.BufferedSnapshotBytes);
        using var retry = receiver.Receive(fragment);
        Assert.NotNull(retry);
    }

    private sealed class ConnectedStub : IMessageConnection
    {
        public bool IsConnected => true;

        public bool IsInProcess => false;

        public ConnectionSendResult TrySendOwned(OutboundPackage message) => ConnectionSendResult.Accepted;

        public void Disconnect(LocalizedText reason)
        {
        }

        public void Dispose()
        {
        }
    }

    private static void Receive(
        NativeTerrainReceiver receiver,
        ClientChunkContentTransport transport,
        EncodedTerrainChunkFragment fragment
    )
    {
        using var message = receiver.Receive(fragment);
        if (message != null) { transport.Receive(message.Snapshot, message.Reservation); }
    }

    [Fact]
    public void LocalCaptureAndNativeDecodePublishIdenticalOwnedContent()
    {
        var allocation = new ChunkAllocationId(new Point2(4, 5), 7);
        var cells = Enumerable.Repeat(Terrain.MakeBlockValue(7, 15, 3), AuthorityChunkSnapshot.CellCount).ToArray();
        var shaft = Terrain.ReplaceTemperature(Terrain.ReplaceHumidity(-1L, 9), 3);
        var shafts = Enumerable.Repeat(shaft, AuthorityChunkSnapshot.ShaftCount).ToArray();
        var authority = new AuthorityChunkSnapshot(allocation.Coords, 2, cells, shafts);
        var local = ClientChunkSnapshot.Capture(allocation, authority);
        var encoded = NetworkChunkCodec.Encode(authority);
        var native = NetworkChunkCodec.DecodeSnapshot(allocation, 2, encoded.Payload);
        Assert.Equal(native.Cells.ToArray(), local.Cells.ToArray());
        Assert.Equal(native.Shafts.ToArray(), local.Shafts.ToArray());
        cells[0] = 0;
        shafts[0] = 0;
        Assert.Equal(Terrain.MakeBlockValue(7, 0, 3), local.Cells.Span[0]);
        Assert.Equal(3, Terrain.ExtractTemperature(local.Shafts.Span[0]));
        Assert.Equal(9, Terrain.ExtractHumidity(local.Shafts.Span[0]));
        Assert.Throws<ArgumentException>(() => ClientChunkSnapshot.Capture(
                allocation with { Coords = new Point2(0, 0) },
                authority
            )
        );
    }

    [Fact]
    public void DiscardedAllocationsAndClosedWorldsCannotRecreatePendingTransfers()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var transport = new ClientChunkContentTransport(node.Runtime);
        using var receiver = new NativeTerrainReceiver(node.Runtime, transport);
        var old = new ChunkAllocationId(new Point2(7, 8), 1);
        var fragments = EncodedTerrainChunkFragmenter.Split(new EncodedTerrainChunk(old.Coords, 1, new byte[1000]), old)
            .ToArray();
        Receive(receiver, transport, fragments[0]);
        Assert.Equal(0, receiver.PendingTransferCount);
        transport.Request([new ChunkContentRequest(old)]);
        Receive(receiver, transport, fragments[0]);
        Assert.True(receiver.PendingTransferBytes > 0);
        transport.Discard(old);
        foreach (var fragment in fragments) { Receive(receiver, transport, fragment); }

        Assert.Equal(0, receiver.PendingTransferCount);
        Assert.Equal(0, receiver.PendingTransferBytes);

        var current = old with { Generation = 2 };
        transport.Request([new ChunkContentRequest(current)]);
        foreach (var fragment in fragments) { Receive(receiver, transport, fragment); }

        Assert.Equal(0, receiver.PendingTransferCount);
        var encoded = NetworkChunkCodec.Encode(
            new AuthorityChunkSnapshot(
                current.Coords,
                2,
                new int[AuthorityChunkSnapshot.CellCount],
                new long[AuthorityChunkSnapshot.ShaftCount]
            )
        );
        foreach (var fragment in EncodedTerrainChunkFragmenter.Split(encoded, current))
        {
            Receive(receiver, transport, fragment);
        }

        transport.Discard(old);
        Assert.True(transport.PendingSnapshotBytes > 0);
        var other = new ChunkAllocationId(new Point2(20, 20), 1);
        transport.Request([new ChunkContentRequest(other)]);
        Receive(
            receiver,
            transport,
            EncodedTerrainChunkFragmenter.Split(new EncodedTerrainChunk(other.Coords, 1, new byte[1000]), other).First()
        );
        Assert.True(receiver.PendingTransferBytes > 0);
        node.Runtime.Close();
        Assert.Equal(0, transport.PendingSnapshotBytes);
        Assert.Equal(0, receiver.PendingTransferBytes);
        foreach (var fragment in EncodedTerrainChunkFragmenter.Split(encoded, current))
        {
            Receive(receiver, transport, fragment);
        }

        Assert.Equal(0, transport.DrainReceived(new List<ClientChunkSnapshot>()));
    }

    [Fact]
    public void CompletedSnapshotBackpressureIsBoundedAndDroppedCompletionsRemainRetryable()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var transport = new ClientChunkContentTransport(node.Runtime);
        using var receiver = new NativeTerrainReceiver(node.Runtime, transport);
        var allocations = Enumerable.Range(0, 66).Select(x => new ChunkAllocationId(new Point2(x, 0), 1)).ToArray();
        transport.Request(allocations.Select(allocation => new ChunkContentRequest(allocation)).ToArray());
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        var shafts = new long[AuthorityChunkSnapshot.ShaftCount];
        foreach (var allocation in allocations)
        {
            var encoded = NetworkChunkCodec.Encode(new AuthorityChunkSnapshot(allocation.Coords, 1, cells, shafts));
            foreach (var fragment in EncodedTerrainChunkFragmenter.Split(encoded, allocation))
            {
                Receive(receiver, transport, fragment);
            }
        }

        Assert.InRange(transport.PendingSnapshotBytes, 1, ClientChunkContentTransport.MaximumQueuedSnapshotBytes);
        var snapshots = new List<ClientChunkSnapshot>();
        var count = transport.DrainReceived(snapshots);
        Assert.InRange(count, 1, 65);
        var rejected = allocations.First(allocation => snapshots.All(snapshot => snapshot.Allocation != allocation));
        var sent = transport.RequestsSubmitted;
        transport.Request([new ChunkContentRequest(rejected)]);
        Assert.Equal(sent + 1, transport.RequestsSubmitted);
        var retry = NetworkChunkCodec.Encode(new AuthorityChunkSnapshot(rejected.Coords, 1, cells, shafts));
        foreach (var fragment in EncodedTerrainChunkFragmenter.Split(retry, rejected))
        {
            Receive(receiver, transport, fragment);
        }

        var restored = new List<ClientChunkSnapshot>();
        Assert.Equal(1, transport.DrainReceived(restored));
        Assert.Equal(rejected, restored[0].Allocation);
    }

    [Fact]
    public void DecodeFailureDoesNotPermanentlySuppressTheSameTransferVersion()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var transport = new ClientChunkContentTransport(node.Runtime);
        using var receiver = new NativeTerrainReceiver(node.Runtime, transport);
        var allocation = new ChunkAllocationId(new Point2(0, 0), 1);
        transport.Request([new ChunkContentRequest(allocation)]);
        var bad = EncodedTerrainChunkFragmenter.Split(
            new EncodedTerrainChunk(allocation.Coords, 1, new byte[10]),
            allocation
        ).Single();
        Assert.Throws<InvalidDataException>(() => Receive(receiver, transport, bad));
        var good = NetworkChunkCodec.Encode(
            new AuthorityChunkSnapshot(
                allocation.Coords,
                1,
                new int[AuthorityChunkSnapshot.CellCount],
                new long[AuthorityChunkSnapshot.ShaftCount]
            )
        );
        foreach (var fragment in EncodedTerrainChunkFragmenter.Split(good, allocation))
        {
            Receive(receiver, transport, fragment);
        }

        Assert.Equal(1, transport.DrainReceived(new List<ClientChunkSnapshot>()));
    }
}
