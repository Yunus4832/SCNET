using Engine.Core.Mathematics;

using Game.Protocol.Terrain;
using Game.Subsystems;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class ClientTerrainContentPumpTest
{
    [Fact]
    public void DeliveryReadsAppliedDemandAndAppliesThroughTheOwnedApplication()
    {
        using var terrain = new Terrain();
        var world = new SubsystemTerrain { Terrain = terrain };
        using var executor = world.WorkExecutor;
        using var demands = new TerrainDemandManager(terrain, TerrainContentRole.Replica);
        typeof(SubsystemTerrain).GetProperty(nameof(SubsystemTerrain.Demands))!.SetValue(world, demands);
        demands.SetLocation(0, new Vector2(8, 8), 32, 32);
        demands.SetLocation(-2, new Vector2(100, 100), 16, 16);
        demands.Apply(demands.CapturePending());
        // Queued but unapplied demand is not a second source of delivery priority.
        demands.SetLocation(0, new Vector2(200, 200), 32, 32);
        var chunk = terrain.AllocateChunk(0, 0);
        var transport = new RecordingTransport();
        var cells = new int[AuthorityChunkSnapshot.CellCount];
        cells[0] = 42;
        transport.Snapshots.Enqueue(
            new ClientChunkSnapshot(
                new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration),
                1,
                cells,
                new long[AuthorityChunkSnapshot.ShaftCount]
            )
        );
        using var pump = new ClientTerrainContentPump(
            world,
            transport,
            new ClientChunkContentCoordinator(terrain),
            new ClientTerrainDeltaCoordinator(terrain, _ => { })
        );

        Assert.True(pump.TryDrainReceivedContents());
        Assert.Equal([new Vector2(8, 8)], transport.PriorityCenters);
        Assert.True(chunk.HasContents);
        Assert.Equal(42, chunk.Cells[0]);
        Assert.False(executor.HasMainRequest);
        Assert.True(pump.TryDrainReceivedContents());
    }

    [Fact]
    public void FreshRequestsDoNotWaitForAClockBoundaryAndOnlySubmittedChunksStartTheirRetryWindow()
    {
        using var terrain = new Terrain();
        for (var x = 99; x >= 0; x--) { terrain.AllocateChunk(x, 0); }

        var locations = new[] { new TerrainUpdateLocation { Center = new Vector2(8, 8), ContentDistance = 2048 } };
        var transport = new RecordingTransport();

        Assert.Equal(
            64,
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 0)
        );
        Assert.Equal(Enumerable.Range(0, 64), transport.Requests.Select(request => request.Allocation.Coords.X));
        Assert.False(terrain.GetChunkAtCoords(99, 0)!.IsRequested);
        Assert.Equal(
            36,
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 0.01)
        );
        Assert.Equal(
            0,
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 4.999)
        );
        Assert.Equal(
            64,
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 5)
        );
        Assert.Equal(2, transport.Requests.Count(request => request.Allocation.Coords == new Point2(0, 0)));
    }

    [Fact]
    public void ChangedInterestAndReleasedAllocationsCannotLeaveStaleRequestsInALaterBatch()
    {
        using var terrain = new Terrain();
        for (var x = 0; x < 100; x++) { terrain.AllocateChunk(x, 0); }

        var transport = new RecordingTransport();
        var locations = new[] { new TerrainUpdateLocation { Center = new Vector2(8, 8), ContentDistance = 2048 } };
        ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1);
        transport.Requests.Clear();
        locations[0].Center = new Vector2(1592, 8);
        locations[0].ContentDistance = 16;
        var old = terrain.GetChunkAtCoords(99, 0)!;
        var generation = old.AllocationGeneration;
        terrain.FreeChunk(old);
        old.Dispose();
        var replacement = terrain.AllocateChunk(99, 0);

        ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1.1);

        Assert.Equal([99, 98, 97], transport.Requests.Select(request => request.Allocation.Coords.X));
        Assert.True(replacement.AllocationGeneration > generation);
        Assert.Equal(replacement.AllocationGeneration, transport.Requests[0].Allocation.Generation);
        Assert.False(terrain.GetChunkAtCoords(64, 0)!.IsRequested);
    }

    [Fact]
    public void FailedSubmissionDoesNotMarkChunksAsRequested()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var transport = new RecordingTransport { Fail = true };
        var locations = new[] { new TerrainUpdateLocation { Center = chunk.Center, ContentDistance = 64 } };

        Assert.Throws<InvalidOperationException>(() =>
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1)
        );
        Assert.False(chunk.IsRequested);
        transport.Fail = false;
        Assert.Equal(
            1,
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1.01)
        );
    }

    [Fact]
    public void LoadedReplicaRefreshUsesTheNormalRequestAndRetryScheduleWithoutResettingItsDerivedState()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        var derivedRequest = chunk.DerivedRevisions.Capture();
        Assert.True(chunk.DerivedRevisions.PublishHeights(derivedRequest));
        Assert.True(chunk.DerivedRevisions.PublishSunlight(derivedRequest));
        Assert.True(chunk.DerivedRevisions.PublishLight(derivedRequest));
        chunk.NetworkContentVersion = 10;


        var locations = new[] { new TerrainUpdateLocation { Center = chunk.Center, ContentDistance = 64 } };
        var transport = new RecordingTransport();
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => { });
        var request = new ChunkContentResync(new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration), 12);
        Assert.Equal(
            0,
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1)
        );
        coordinator.RequireSnapshot(request);
        Assert.Equal(
            1,
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1)
        );
        Assert.Equal(10, Assert.Single(transport.Requests).KnownContentVersion);
        coordinator.RequireSnapshot(request);
        Assert.Equal(
            0,
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1.1)
        );
        Assert.Equal(
            1,
            ClientTerrainContentPump.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 6)
        );
        Assert.True(chunk.HasContents);
        Assert.True(chunk.HasCurrentHeightData);
        Assert.True(chunk.HasCurrentLight);
    }

    [Fact]
    public void NetworkChunkRequestRetriesOnlyAfterRecoveryWindow()
    {
        var chunk = new TerrainChunk(null!, 1, 2)
        {
            IsRequested = true,
            NetworkRequestTime = 10.0
        };

        Assert.False(ClientTerrainContentPump.ShouldRequestNetworkChunk(chunk, 14.999));
        Assert.True(ClientTerrainContentPump.ShouldRequestNetworkChunk(chunk, 15.0));
    }

    [Fact]
    public void NetworkChunkRequestRetriesAfterElapsedTimeOrClockRollback()
    {
        var chunk = new TerrainChunk(null!, 1, 2) { IsRequested = true };

        Assert.True(ClientTerrainContentPump.ShouldRequestNetworkChunk(chunk, 20.0));

        chunk.NetworkRequestTime = 30.0;
        Assert.True(ClientTerrainContentPump.ShouldRequestNetworkChunk(chunk, 20.0));
    }

    private sealed class RecordingTransport : IChunkContentTransport
    {
        public readonly Queue<ClientChunkSnapshot> Snapshots = new();

        public IReadOnlyList<Vector2> PriorityCenters = [];

        public bool HasPendingWork => Snapshots.Count > 0;

        public void SetPriorityCenters(IReadOnlyList<Vector2> centers) => PriorityCenters = centers;

        public readonly List<ChunkContentRequest> Requests = [];

        public bool Fail;

        public void Request(IReadOnlyList<ChunkContentRequest> requests)
        {
            if (Fail) { throw new InvalidOperationException("Test submission failure."); }

            Requests.AddRange(requests);
        }

        public void Discard(ChunkAllocationId allocation)
        {
        }

        public int DrainReceived(ICollection<ClientChunkSnapshot> destination, int maximumCount = int.MaxValue)
        {
            var count = 0;
            while (count < maximumCount && Snapshots.TryDequeue(out var snapshot))
            {
                destination.Add(snapshot);
                count++;
            }

            return count;
        }

        public int DrainDeltas(ICollection<TerrainCellDelta> destination, int maximumCount = 1024) => 0;

        public int DrainResyncRequests(ICollection<ChunkContentResync> destination, int maximumCount = int.MaxValue)
        {
            return 0;
        }
    }
}
