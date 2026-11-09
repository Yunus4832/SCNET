using System.Reflection;

using Engine.Core.Mathematics;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Chunks;
using Game.Network.Replication;
using Game.Network.Transport;
using Game.Terrains.Distribution;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains.Distribution;

[Collection(ConfigFileCollection.Name)]
public sealed class ServerChunkDistributionSchedulerTest : IDisposable
{
    private readonly Project _project = new();

    private readonly NetNode _node = new NetNode(
        NetNodeOptions.Default,
        true,
        Game.Network.Handling.PackageHandlerCatalog.Empty,
        NodeRole.Authority
    );

    public ServerChunkDistributionSchedulerTest() => _node.Runtime.BindProject(_project);

    public void Dispose() => _project.Dispose();

    [Fact]
    public void ReplacementAllocationsCancelOwnedOutboxDataAndRejectLateOlderRequests()
    {
        using var scheduler = new ServerChunkDistributionScheduler(_project, new SnapshotAuthority());
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
        _node.AddClient(client);
        var old = new ChunkAllocationId(new Point2(0, 0), 1);
        scheduler.UpdateClientLocation(client, Vector2.Zero, 64);
        scheduler.Enqueue(client, [new ChunkContentRequest(old)]);
        scheduler.Update();
        Assert.Equal(1, _node.PendingPackageCount);
        var current = old with { Generation = 2 };
        Assert.Equal(1, scheduler.Enqueue(client, [new ChunkContentRequest(current)]));
        Assert.Equal(0, _node.PendingPackageCount);
        scheduler.Update();
        Assert.Equal(0, scheduler.Enqueue(client, [new ChunkContentRequest(old)]));
        Assert.True(scheduler.IsCurrentAllocation(client, current));
        Assert.False(scheduler.IsCurrentAllocation(client, old));
        var pending = (PendingLogicalMessages)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_node)!;
        Assert.Equal(
            current,
            Assert.IsType<TerrainSnapshotMessage>(Assert.Single(pending).Package).Snapshot.Allocation
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothConnectionKindsPublishOwnedLogicalSnapshotsWithoutEncoding(bool inProcess)
    {
        var authority = new SnapshotAuthority();
        using var scheduler = new ServerChunkDistributionScheduler(_project, authority);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
        _node.AddClient(client);
        client.Connection = new ConnectedStub(inProcess);
        scheduler.UpdateClientLocation(client, new Vector2(8), 256);
        scheduler.Enqueue(
            client,
            Enumerable.Range(0, 8).Select(x =>
                new ChunkContentRequest(new ChunkAllocationId(new Point2(x, 0), 1))
            )
        );
        scheduler.Update();
        var queued = (PendingLogicalMessages)typeof(NetNode)
            .GetField("_pendingPackages", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_node)!;
        var snapshots = queued.Select(value => value.Package)
            .OfType<TerrainSnapshotMessage>().ToArray();
        Assert.Equal(4, snapshots.Length);
        Assert.DoesNotContain(queued, value => value.Package is TerrainFragmentPackage);
        Assert.Equal(0, _node.OutstandingChunkEncodes);
        Assert.Equal(4, scheduler.GetPendingCount(client));
        authority.Cells[0] = 99;
        authority.Shafts[0] = 88;
        Assert.All(
            snapshots,
            message =>
            {
                Assert.Equal(0, message.Snapshot.Cells.Span[0]);
                Assert.Equal(0, message.Snapshot.Shafts.Span[0]);
                Assert.Equal(1, message.Snapshot.ContentVersion);
            }
        );
        scheduler.Update();
        Assert.Equal(4, scheduler.GetPendingCount(client));
        Assert.Equal(4, queued.Count);
    }

    private sealed class ConnectedStub(bool inProcess) : IMessageConnection
    {
        public bool IsConnected => true;

        public bool IsInProcess => inProcess;

        public ConnectionSendResult TrySendOwned(OutboundPackage message) => ConnectionSendResult.Accepted;

        public void Disconnect(LocalizedText reason)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class SnapshotAuthority : IChunkContentAuthority
    {
        public int[] Cells { get; } = new int[AuthorityChunkSnapshot.CellCount];

        public long[] Shafts { get; } = new long[AuthorityChunkSnapshot.ShaftCount];

        public bool TryGetDescriptor(Point2 coords, out AuthorityChunkDescriptor descriptor)
        {
            descriptor = new AuthorityChunkDescriptor(coords, 1);
            return true;
        }

        public bool TryGetSnapshot(Point2 coords, out AuthorityChunkSnapshot snapshot)
        {
            snapshot = new AuthorityChunkSnapshot(coords, 1, Cells, Shafts);
            return true;
        }
    }

    [Fact]
    public void AClientWorldCannotCreateAnAuthorityScheduler()
    {
        using var project = new Project();
        new WorldRuntimeContext(
            RuntimeSide.Client,
            (true)
        ).BindProject(project);
        Assert.Throws<InvalidOperationException>(() =>
            new ServerChunkDistributionScheduler(project, new EmptyAuthority())
        );
    }

    [Fact]
    public void UnreadyContentRetainsRequestsUntilInterestMovesAway()
    {
        using var scheduler = new ServerChunkDistributionScheduler(_project, new EmptyAuthority());
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        _node.AddClient(client);
        var request = new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1));
        scheduler.UpdateClientLocation(client, Vector2.Zero, 64f);
        scheduler.Enqueue(client, [request]);

        scheduler.Update();

        Assert.Equal(1, scheduler.GetPendingCount(client));
        Assert.Equal((1, 1), scheduler.GetBacklog());
        scheduler.UpdateClientLocation(client, new Vector2(1000f), 64f);
        Assert.Equal(0, scheduler.GetPendingCount(client));
    }

    [Fact]
    public void ReadyContentCanBeCapturedWithoutWaitingForAWholeSecond()
    {
        var authority = new CountingAuthority();
        using var scheduler = new ServerChunkDistributionScheduler(_project, authority);
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        _node.AddClient(client);
        scheduler.UpdateClientLocation(client, Vector2.Zero, 64f);
        scheduler.Enqueue(client, [new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1))]);

        scheduler.Update();
        Assert.Equal(1, scheduler.GetPendingCount(client));
        Assert.Equal(0, authority.SnapshotAttempts);

        authority.Ready = true;
        scheduler.Update();

        Assert.Equal(1, scheduler.GetPendingCount(client));
        Assert.Equal(1, authority.SnapshotAttempts);
        Assert.Equal((1, 0), scheduler.GetBacklog());
    }

    [Theory]
    [InlineData(2, 0, true, false)]
    [InlineData(2, 1, false, false)]
    [InlineData(-3, -1, true, false)]
    [InlineData(-3, -2, false, false)]
    [InlineData(1, 0, true, true)]
    [InlineData(-2, -1, true, true)]
    public void ContentRequestsUseCentersWhileSpatialBroadphaseKeepsChunkOverlap(
        int x,
        int z,
        bool broadphase,
        bool expected
    )
    {
        using var scheduler = new ServerChunkDistributionScheduler(_project, new EmptyAuthority());
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        _node.AddClient(client);
        var coords = new Point2(x, z);
        var request = new ChunkContentRequest(new ChunkAllocationId(coords, 1));
        var index = new SpatialInterestIndex();
        index.SetLocation(client, Vector2.Zero, 32f);
        scheduler.UpdateClientLocation(client, Vector2.Zero, 32f);

        Assert.Equal(broadphase, index.GetChunkObservers(coords).Contains(client));
        Assert.Equal(expected ? 1 : 0, scheduler.Enqueue(client, [request]));
        Assert.Equal(expected ? 1 : 0, scheduler.GetPendingCount(client));
        var queue = new PendingChunkRequestQueue();
        queue.Enqueue(request);
        Assert.Equal(expected ? 0 : 1, queue.RemoveOutside(Vector2.Zero, 32f));

        scheduler.UpdateClientLocation(client, new Vector2(1000f), 32f);
        Assert.Equal(0, scheduler.GetPendingCount(client));
    }

    [Fact]
    public void MovingAwayDropsPendingContentEvenWhenItsBoxStillTouchesInterest()
    {
        using var scheduler = new ServerChunkDistributionScheduler(_project, new EmptyAuthority());
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
        _node.AddClient(client);
        var allocation = new ChunkAllocationId(new Point2(2, 1), 1);
        scheduler.UpdateClientLocation(client, new Vector2(16, 8), 32);
        Assert.Equal(1, scheduler.Enqueue(client, [new ChunkContentRequest(allocation)]));
        Assert.Equal((1, 1), scheduler.GetBacklog());

        var moved = new Vector2(7, 8);
        Assert.True(NetworkTerrainPolicy.IsChunkRelevant(allocation.Coords, moved, 32));
        scheduler.UpdateClientLocation(client, moved, 32);
        Assert.Equal(0, scheduler.GetPendingCount(client));
        Assert.False(scheduler.IsCurrentAllocation(client, allocation));
        Assert.Equal(0, scheduler.Enqueue(client, [new ChunkContentRequest(allocation)]));
        Assert.Equal((0, 0), scheduler.GetBacklog());
    }

    [Fact]
    public void MovingAwayReleasesOwnedSnapshotOutsideTheContentCenterCircle()
    {
        using var scheduler = new ServerChunkDistributionScheduler(_project, new SnapshotAuthority());
        var client = new Client(null, 1, Guid.NewGuid(), Guid.Empty);
        _node.AddClient(client);
        var allocation = new ChunkAllocationId(new Point2(2, 1), 1);
        scheduler.UpdateClientLocation(client, new Vector2(16, 8), 32);
        scheduler.Enqueue(client, [new ChunkContentRequest(allocation)]);
        scheduler.Update();
        Assert.Equal(1, _node.PendingPackageCount);

        scheduler.UpdateClientLocation(client, new Vector2(7, 8), 32);
        Assert.Equal(0, _node.PendingPackageCount);
        Assert.False(scheduler.IsCurrentAllocation(client, allocation));
    }

    [Fact]
    public void ReusedConnectionIdentityKeepsIndependentQueuesAndDisconnectCleanup()
    {
        using var scheduler = new ServerChunkDistributionScheduler(_project, new EmptyAuthority());
        var old = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        var current = new Client(null, old.ID, Guid.NewGuid(), old.CharacterId);
        var request = new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1));
        _node.AddClient(old);
        scheduler.UpdateClientLocation(old, Vector2.Zero, 64f);
        Assert.Equal(1, scheduler.Enqueue(old, [request]));
        // A new transport record takes the routing slot while the old scheduler lease awaits cleanup.
        _node.AddClient(current);
        scheduler.UpdateClientLocation(current, Vector2.Zero, 64f);
        Assert.Equal(1, scheduler.Enqueue(current, [request]));
        Assert.Equal(2, scheduler.ClientCount);
        scheduler.RemoveClient(old);

        Assert.Equal(0, scheduler.GetPendingCount(old));
        Assert.Equal(1, scheduler.GetPendingCount(current));
        Assert.True(scheduler.TryGetClientLocation(current, out _, out _));
        Assert.Equal(1, scheduler.ClientCount);
    }

    [Fact]
    public void RequestsRequireApprovedLocationAndRejectDistantChunks()
    {
        using var scheduler = new ServerChunkDistributionScheduler(_project, new EmptyAuthority());
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        _node.AddClient(client);
        var near = new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1));
        var far = new ChunkContentRequest(new ChunkAllocationId(new Point2(100, 100), 1));

        Assert.Equal(0, scheduler.Enqueue(client, [near]));
        scheduler.UpdateClientLocation(client, Vector2.Zero, 64f);
        Assert.Equal(1, scheduler.Enqueue(client, [near, far]));
        Assert.Equal(1, scheduler.GetPendingCount(client));
    }

    [Fact]
    public void EmptyQueueRetainsApprovedLocationUntilDisconnect()
    {
        using var scheduler = new ServerChunkDistributionScheduler(_project, new EmptyAuthority());
        var client = new Client(null, 1, Guid.NewGuid(), Guid.NewGuid());
        _node.AddClient(client);
        var request = new ChunkContentRequest(new ChunkAllocationId(new Point2(0, 0), 1));
        Assert.False(scheduler.TryGetClientLocation(client, out _, out _));
        scheduler.UpdateClientLocation(client, Vector2.Zero, 64f);
        Assert.Equal(0, scheduler.Enqueue(client, []));

        scheduler.Update();

        Assert.True(scheduler.TryGetClientLocation(client, out var center, out var distance));
        Assert.Equal(Vector2.Zero, center);
        Assert.Equal(64f, distance);
        Assert.Equal(1, scheduler.Enqueue(client, [request]));
        scheduler.RemoveClient(client);
        Assert.False(scheduler.TryGetClientLocation(client, out _, out _));
        Assert.Equal(0, scheduler.Enqueue(client, [request]));
    }

    [Fact]
    public void PredictedCenterPrioritizesChunksInMovementDirection()
    {
        var queue = new PendingChunkRequestQueue();
        queue.Enqueue(new ChunkContentRequest(new ChunkAllocationId(new Point2(-2, 0), 1)));
        queue.Enqueue(new ChunkContentRequest(new ChunkAllocationId(new Point2(2, 0), 1)));

        var selected = Assert.Single(
            queue.TakePrioritized(
                new Vector2(8, 8),
                new Vector2(40, 8),
                1
            )
        );

        Assert.Equal(new Point2(2, 0), selected.Allocation.Coords);
    }

    private sealed class CountingAuthority : IChunkContentAuthority
    {
        public bool Ready { get; set; }

        public int SnapshotAttempts { get; private set; }

        public long ContentVersion { get; set; } = 1;

        public bool TryGetDescriptor(Point2 coords, out AuthorityChunkDescriptor descriptor)
        {
            descriptor = new AuthorityChunkDescriptor(coords, ContentVersion);
            return Ready;
        }

        public bool TryGetSnapshot(Point2 coords, out AuthorityChunkSnapshot snapshot)
        {
            SnapshotAttempts++;
            snapshot = null!;
            return false;
        }
    }

    private sealed class EmptyAuthority : IChunkContentAuthority
    {
        public bool TryGetDescriptor(Point2 coords, out AuthorityChunkDescriptor descriptor)
        {
            descriptor = default;
            return false;
        }

        public bool TryGetSnapshot(Point2 coords, out AuthorityChunkSnapshot snapshot)
        {
            snapshot = null!;
            return false;
        }
    }
}
