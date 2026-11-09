using Engine.Core.Mathematics;

using Game.Protocol.Terrain;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class ClientTerrainDeltaCoordinatorTest
{
    private static TerrainChunk LoadedChunk(Terrain terrain, long version)
    {
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.NetworkContentVersion = version;


        return chunk;
    }

    private static TerrainCellDelta Delta(long baseVersion, long resultVersion, int value) =>
        new(new Point3(1, 2, 3), value, baseVersion, resultVersion);

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

        chunk.HasContents = true;
        chunk.NetworkContentVersion = 10;
        Assert.Equal(TerrainDeltaApplyResult.Applied, coordinator.OnContentInstalled(chunk));
        Assert.Equal(11, chunk.NetworkContentVersion);
        Assert.Equal([delta], applied);
    }

    [Fact]
    public void MissingDeltaCanResolveTheGapWithoutDiscardingAValidReplica()
    {
        using var terrain = new Terrain();
        var chunk = LoadedChunk(terrain, 10);
        var applied = new List<TerrainCellDelta>();
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, applied.Add);
        Assert.Equal(TerrainDeltaApplyResult.ResyncRequired, coordinator.Receive(Delta(11, 12, 42)));
        Assert.Equal(TerrainDeltaApplyResult.Applied, coordinator.Receive(Delta(10, 11, 43)));
        Assert.Equal(12, chunk.NetworkContentVersion);
        Assert.False(chunk.NeedsContentSnapshot);
        Assert.Equal(new[] { 11L, 12L }, applied.Select(delta => delta.ResultContentVersion));
    }

    [Fact]
    public void SnapshotNewerThanBufferedDeltaMakesDeltaStale()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var applied = 0;
        var coordinator = new ClientTerrainDeltaCoordinator(terrain, _ => applied++);

        coordinator.Receive(Delta(10, 11, 42));
        chunk.HasContents = true;
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
}
