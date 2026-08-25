using Engine.Core;

using Game;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class ClientTerrainDeltaCoordinatorTest
{
    [Fact]
    public void ContiguousDeltaAppliesAndAdvancesInstalledVersion()
    {
        var terrain = new Terrain();
        var chunk = LoadedChunk(terrain, 10);
        var applied = new List<TerrainCellDelta>();
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, applied.Add);
        var delta = Delta(10, 11, 42);

        var result = coordinator.Receive(delta);

        Assert.Equal(TerrainDeltaApplyResult.Applied, result);
        Assert.Equal(11, chunk.NetworkContentVersion);
        Assert.Equal([delta], applied);
    }

    [Fact]
    public void DeltaArrivingBeforeSnapshotIsAppliedAfterMatchingSnapshot()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var applied = new List<TerrainCellDelta>();
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, applied.Add);
        var delta = Delta(10, 11, 42);

        Assert.Equal(TerrainDeltaApplyResult.Buffered, coordinator.Receive(delta));

        chunk.IsLoaded = true;
        chunk.NetworkContentVersion = 10;
        Assert.Equal(TerrainDeltaApplyResult.Applied, coordinator.OnContentInstalled(chunk));
        Assert.Equal(11, chunk.NetworkContentVersion);
        Assert.Equal([delta], applied);
    }

    [Fact]
    public void VersionGapInvalidatesReplicaUntilNewSnapshotArrives()
    {
        var terrain = new Terrain();
        var chunk = LoadedChunk(terrain, 10);
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => { });

        var result = coordinator.Receive(Delta(11, 12, 42));

        Assert.Equal(TerrainDeltaApplyResult.ResyncRequired, result);
        Assert.False(chunk.IsLoaded);
        Assert.False(chunk.IsRequested);
        Assert.Equal(TerrainChunkState.NotLoaded, chunk.MainThreadState);
        Assert.Equal(10, chunk.NetworkContentVersion);

        chunk.IsLoaded = true;
        chunk.NetworkContentVersion = 12;
        Assert.Equal(TerrainDeltaApplyResult.Ignored, coordinator.OnContentInstalled(chunk));
        Assert.True(chunk.IsLoaded);
    }

    [Fact]
    public void SnapshotNewerThanBufferedDeltaMakesDeltaStale()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var applied = 0;
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => applied++);

        coordinator.Receive(Delta(10, 11, 42));
        chunk.IsLoaded = true;
        chunk.NetworkContentVersion = 12;

        Assert.Equal(TerrainDeltaApplyResult.Ignored, coordinator.OnContentInstalled(chunk));
        Assert.Equal(0, applied);
    }

    [Fact]
    public void InvalidVersionTransitionIsRejected()
    {
        var terrain = new Terrain();
        terrain.AllocateChunk(0, 0);
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => { });

        Assert.Throws<InvalidDataException>(() => coordinator.Receive(Delta(10, 12, 42)));
    }

    private static TerrainChunk LoadedChunk(Terrain terrain, long version)
    {
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.IsLoaded = true;
        chunk.NetworkContentVersion = version;
        chunk.MainThreadState = TerrainChunkState.Valid;
        chunk.WorkerState = TerrainChunkState.Valid;
        chunk.ClientGeometryContentVersion = version;
        chunk.NetworkGeometryUploaded = true;
        return chunk;
    }

    private static TerrainCellDelta Delta(long baseVersion, long resultVersion, int value) =>
        new(new Point3(1, 2, 3), value, baseVersion, resultVersion);
}
