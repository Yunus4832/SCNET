using Engine.Core.Mathematics;

using Game.Protocol.Terrain;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class ClientChunkRequestSchedulingTest
{
    [Fact]
    public void FreshRequestsDoNotWaitForAClockBoundaryAndOnlySubmittedChunksStartTheirRetryWindow()
    {
        using var terrain = new Terrain();
        for (var x = 99; x >= 0; x--) { terrain.AllocateChunk(x, 0); }

        var locations = new[] { new TerrainUpdateLocation { Center = new Vector2(8, 8), ContentDistance = 2048 } };
        var transport = new RecordingTransport();

        Assert.Equal(64, TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 0));
        Assert.Equal(Enumerable.Range(0, 64), transport.Requests.Select(request => request.Allocation.Coords.X));
        Assert.Equal(0, terrain.GetChunkAtCoords(99, 0)!.NetworkRequestAttempts);
        Assert.Equal(36, TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 0.01));
        Assert.Equal(0, TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 4.999));
        Assert.Equal(64, TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 5));
        Assert.Equal(2, terrain.GetChunkAtCoords(0, 0)!.NetworkRequestAttempts);
    }

    [Fact]
    public void ChangedInterestAndReleasedAllocationsCannotLeaveStaleRequestsInALaterBatch()
    {
        using var terrain = new Terrain();
        for (var x = 0; x < 100; x++) { terrain.AllocateChunk(x, 0); }

        var transport = new RecordingTransport();
        var locations = new[] { new TerrainUpdateLocation { Center = new Vector2(8, 8), ContentDistance = 2048 } };
        TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1);
        transport.Requests.Clear();
        locations[0].Center = new Vector2(1592, 8);
        locations[0].ContentDistance = 16;
        var old = terrain.GetChunkAtCoords(99, 0)!;
        var generation = old.AllocationGeneration;
        terrain.FreeChunk(old);
        old.Dispose();
        var replacement = terrain.AllocateChunk(99, 0);

        TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1.1);

        Assert.Equal([99, 98], transport.Requests.Select(request => request.Allocation.Coords.X));
        Assert.True(replacement.AllocationGeneration > generation);
        Assert.Equal(replacement.AllocationGeneration, transport.Requests[0].Allocation.Generation);
        Assert.Equal(0, terrain.GetChunkAtCoords(64, 0)!.NetworkRequestAttempts);
    }

    [Fact]
    public void FailedSubmissionDoesNotMarkChunksAsRequested()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var transport = new RecordingTransport { Fail = true };
        var locations = new[] { new TerrainUpdateLocation { Center = chunk.Center, ContentDistance = 64 } };

        Assert.Throws<InvalidOperationException>(() =>
            TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1)
        );
        Assert.False(chunk.IsRequested);
        Assert.Equal(0, chunk.NetworkRequestAttempts);
        transport.Fail = false;
        Assert.Equal(1, TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1.01));
    }

    [Fact]
    public void LoadedReplicaRefreshUsesTheNormalRequestAndRetryScheduleWithoutResettingItsDerivedState()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.IsLoaded = true;
        chunk.NetworkContentVersion = 10;
        chunk.SetMainThreadState(TerrainChunkState.Ready);
        chunk.SetWorkerState(TerrainChunkState.Ready);
        var locations = new[] { new TerrainUpdateLocation { Center = chunk.Center, ContentDistance = 64 } };
        var transport = new RecordingTransport();
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => { });
        var request = new ChunkContentResync(new ChunkAllocationId(chunk.Coords, chunk.AllocationGeneration), 12);
        Assert.Equal(0, TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1));
        coordinator.RequireSnapshot(request);
        Assert.Equal(1, TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1));
        Assert.Equal(10, Assert.Single(transport.Requests).KnownContentVersion);
        coordinator.RequireSnapshot(request);
        Assert.Equal(0, TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 1.1));
        Assert.Equal(1, TerrainUpdater.RequestChunkContents(terrain.AllocatedChunks, locations, transport, 6));
        Assert.True(chunk.IsLoaded);
        Assert.Equal(TerrainChunkState.Ready, chunk.MainThreadState);
        Assert.Equal(TerrainChunkState.Ready, chunk.WorkerState);
    }

    private sealed class RecordingTransport : IChunkContentTransport
    {
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

        public int DrainReceived(ICollection<ClientChunkSnapshot> destination, int maximumCount = int.MaxValue) => 0;

        public int DrainDeltas(ICollection<TerrainCellDelta> destination, int maximumCount = 1024) => 0;

        public int DrainResyncRequests(ICollection<ChunkContentResync> destination, int maximumCount = int.MaxValue)
        {
            return 0;
        }
    }
}
