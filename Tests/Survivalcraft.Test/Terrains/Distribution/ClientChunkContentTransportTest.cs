using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Transport;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientChunkContentTransportTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ContinuousArrivalsCannotStarveOldSnapshots(bool prioritizeNearby)
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        using var transport = new ClientChunkContentTransport(node.Runtime);

        void Receive(int x)
        {
            var allocation = new ChunkAllocationId(new Point2(x, 0), 1);
            transport.Request([new ChunkContentRequest(allocation)]);
            transport.Receive(
                new ClientChunkSnapshot(
                    allocation,
                    1,
                    new int[AuthorityChunkSnapshot.CellCount],
                    new long[AuthorityChunkSnapshot.ShaftCount]
                )
            );
        }

        Receive(0);
        Receive(1);
        Receive(2);
        var observed = new List<int>();
        for (var index = 0; index < 20; index++)
        {
            if (prioritizeNearby) { transport.SetPriorityCenters([new Vector2((100 + index) * 16 + 8, 8)]); }

            var drained = new List<ClientChunkSnapshot>();
            Assert.Equal(1, transport.DrainReceived(drained, 1));
            observed.Add(drained[0].Allocation.Coords.X);
            Receive(100 + index);
        }

        Assert.Equal(0, observed[0]);
        Assert.Equal(prioritizeNearby ? 100 : 1, observed[1]);
        Assert.InRange(observed.IndexOf(1), 1, 4);
        Assert.InRange(observed.IndexOf(2), 2, 8);
        node.Runtime.Close();
    }

    [Fact]
    public void SnapshotReplacementKeepsArrivalOrderAndDiscardRemovesTheQueuedEntry()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        using var transport = new ClientChunkContentTransport(node.Runtime);
        var first = new ChunkAllocationId(new Point2(0, 0), 1);
        var second = new ChunkAllocationId(new Point2(1, 0), 1);
        transport.Request([new ChunkContentRequest(first), new ChunkContentRequest(second)]);

        ClientChunkSnapshot Snapshot(ChunkAllocationId allocation, long version) => new(
            allocation,
            version,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]
        );

        transport.Receive(Snapshot(first, 1));
        transport.Receive(Snapshot(second, 1));
        transport.Receive(Snapshot(first, 2));
        var drained = new List<ClientChunkSnapshot>();
        Assert.Equal(1, transport.DrainReceived(drained, 1));
        Assert.Equal(first, drained[0].Allocation);
        Assert.Equal(2, drained[0].ContentVersion);

        transport.Discard(second);
        Assert.False(transport.HasPendingWork);
        transport.Receive(Snapshot(first, 3));
        Assert.True(transport.HasPendingWork);
        drained.Clear();
        Assert.Equal(1, transport.DrainReceived(drained));
        Assert.Equal(3, drained[0].ContentVersion);
        Assert.False(transport.HasPendingWork);
        node.Runtime.Close();
    }

    [Fact]
    public void OnlyOneClientContentQueueCanOwnARuntimeAndCloseReleasesTheBinding()
    {
        var authority = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Authority
        );
        Assert.Throws<InvalidOperationException>(() => new ClientChunkContentTransport(authority.Runtime));
        var client = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        using var content = new ClientChunkContentTransport(client.Runtime);
        Assert.Same(content, ClientChunkContentTransport.For(client.Runtime));
        Assert.Throws<InvalidOperationException>(() => new ClientChunkContentTransport(client.Runtime));
        Assert.Same(content, ClientChunkContentTransport.For(client.Runtime));
        client.Runtime.Close();
        Assert.Throws<InvalidOperationException>(() => ClientChunkContentTransport.For(client.Runtime));
        content.Dispose();
        content.Dispose();
        authority.Runtime.Close();
    }

    [Fact]
    public void LogicalSnapshotsRequireLiveAllocationsAndCannotReplaceNewerQueuedContent()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var transport = new ClientChunkContentTransport(node.Runtime);
        var allocation = new ChunkAllocationId(new Point2(7, 8), 1);

        ClientChunkSnapshot Snapshot(long version) => new(
            allocation,
            version,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]
        );

        transport.Receive(Snapshot(1));
        Assert.Equal(0, transport.PendingSnapshotBytes);
        transport.Request([new ChunkContentRequest(allocation)]);
        transport.Receive(Snapshot(2));
        transport.Receive(Snapshot(1));
        var received = new List<ClientChunkSnapshot>();
        Assert.Equal(1, transport.DrainReceived(received));
        Assert.Equal(2, received[0].ContentVersion);
        transport.Discard(allocation);
        transport.Receive(Snapshot(3));
        Assert.Equal(0, transport.PendingSnapshotBytes);
        transport.Request([new ChunkContentRequest(allocation)]);
        node.Runtime.Close();
        transport.Receive(Snapshot(4));
        Assert.Equal(0, transport.PendingSnapshotBytes);
    }

    [Fact]
    public void LargeRequestListsAreSplitIntoProtocolSizedOwnedMessagesAndClosedWorldsSendNothing()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var transport = new ClientChunkContentTransport(node.Runtime);
        var requests = Enumerable.Range(0, 130)
            .Select(x => new ChunkContentRequest(new ChunkAllocationId(new Point2(x, 0), 1))).ToArray();

        transport.Request(requests);
        var pending = (PendingLogicalMessages)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node)!;
        var packages = pending.Select(outbound => Assert.IsType<TerrainChunksRequestPackage>(outbound.Package))
            .ToArray();
        Assert.Equal([64, 64, 2], packages.Select(package => package.ChunkRequests.Count));
        Assert.Equal(requests, packages.SelectMany(package => package.ChunkRequests));
        Assert.All(packages, ProtocolMessageValidation.Validate);
        Array.Clear(requests);
        Assert.Equal(1UL, packages[0].ChunkRequests[0].Allocation.Generation);
        Assert.Equal(130, transport.RequestsSubmitted);

        node.Runtime.Close();
        transport.Request([new ChunkContentRequest(new ChunkAllocationId(new Point2(140, 0), 1))]);
        Assert.Empty(pending);
        Assert.Equal(130, transport.RequestsSubmitted);
    }

    [Fact]
    public void ReceiveQueuesSnapshotsAndDeltasUntilUpdateLoopDrainsThem()
    {
        using var project = new Project();
        var node = new NetNode(
            NetNodeOptions.Default,
            true,
            Game.Network.Handling.PackageHandlerCatalog.Empty,
            NodeRole.Client
        );
        node.Runtime.BindProject(project);
        node.Server = new Client(Guid.Empty);
        var transport = new ClientChunkContentTransport(node.Runtime);
        var allocation = new ChunkAllocationId(new Point2(7, 8), 4);
        var snapshot = new AuthorityChunkSnapshot(
            allocation.Coords,
            2,
            new int[AuthorityChunkSnapshot.CellCount],
            new long[AuthorityChunkSnapshot.ShaftCount]
        );
        transport.Request([new ChunkContentRequest(allocation)]);

        transport.Receive(ClientChunkSnapshot.Capture(allocation, snapshot));

        var delta = new TerrainCellDelta(new Point3(7 * 16 + 1, 2, 8 * 16 + 3), 4, 2, 3);
        transport.Receive(delta);
        var snapshots = new List<ClientChunkSnapshot>();
        var deltas = new List<TerrainCellDelta>();

        Assert.Equal(1, transport.DrainReceived(snapshots));
        Assert.Equal(1, transport.DrainDeltas(deltas));
        Assert.Equal(snapshot.ContentVersion, snapshots[0].ContentVersion);
        Assert.Equal(delta, deltas[0]);
        Assert.Equal(0, transport.DrainReceived(snapshots));
        Assert.Equal(0, transport.DrainDeltas(deltas));
    }
}
