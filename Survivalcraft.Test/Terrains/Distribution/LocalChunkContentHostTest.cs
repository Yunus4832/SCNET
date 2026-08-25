using Engine.Core;

using Game.Terrains;
using Game.Terrains.Distribution;
using Game.TerrainSerializers;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class LocalChunkContentHostTest
{
    [Fact]
    public void HostGeneratesFairlyAndPreservesClientAllocationIdentity()
    {
        var authorityTerrain = new Terrain();
        var host = CreateHost(authorityTerrain);
        var first = new ChunkContentRequest(new ChunkAllocationId(new Point2(1, 2), 9));
        var second = new ChunkContentRequest(new ChunkAllocationId(new Point2(3, 4), 12));
        host.Request([first, second, first]);

        Assert.Equal(2, host.PendingCount);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(2, host.Update(2));
        }

        var received = new List<ClientChunkSnapshot>();
        Assert.Equal(2, host.DrainReceived(received));
        Assert.Contains(received, value => value.Allocation == first.Allocation);
        Assert.Contains(received, value => value.Allocation == second.Allocation);
        Assert.All(received, value => Assert.Equal(42, value.Cells.Span[0]));
        Assert.Equal(0, host.PendingCount);
    }

    [Fact]
    public void KnownVersionDoesNotProduceRedundantLocalDelivery()
    {
        var authorityTerrain = new Terrain();
        var host = CreateHost(authorityTerrain);
        var coords = new Point2(1, 2);
        host.Request([new ChunkContentRequest(new ChunkAllocationId(coords, 1), long.MaxValue)]);

        for (var i = 0; i < 5; i++)
        {
            host.Update(1);
        }

        Assert.Equal(0, host.DrainReceived(new List<ClientChunkSnapshot>()));
        Assert.Equal(0, host.PendingCount);
    }

    [Fact]
    public void ReleaseRemovesAuthorityAllocationAndPendingRequest()
    {
        var authorityTerrain = new Terrain();
        var host = CreateHost(authorityTerrain);
        var coords = new Point2(5, 6);
        host.Request([new ChunkContentRequest(new ChunkAllocationId(coords, 1))]);
        host.Update(1);

        Assert.True(host.Release(coords));

        Assert.Null(authorityTerrain.GetChunkAtCoords(coords.X, coords.Y));
        Assert.Equal(0, host.PendingCount);
    }

    [Fact]
    public void CellWritesTargetAuthorityTerrainInsteadOfClientReplica()
    {
        using var authorityTerrain = new Terrain();
        using var clientTerrain = new Terrain();
        authorityTerrain.AllocateChunk(0, 0);
        clientTerrain.AllocateChunk(0, 0);
        var host = CreateHost(authorityTerrain);

        Assert.True(host.CellAuthority.ChangeCell(2, 3, 4, 57));

        Assert.Equal(57, authorityTerrain.GetCellValue(2, 3, 4));
        Assert.Equal(0, clientTerrain.GetCellValue(2, 3, 4));
    }

    [Fact]
    public void ReleaseKeepsAuthorityChunkWhenPersistenceBackpressureRejectsIt()
    {
        using var authorityTerrain = new Terrain();
        var coords = new Point2(7, 8);
        var chunk = authorityTerrain.AllocateChunk(coords.X, coords.Y);
        var attempts = 0;
        var host = CreateHost(authorityTerrain, candidate =>
        {
            Assert.Same(chunk, candidate);
            attempts++;
            return false;
        });
        host.Request([new ChunkContentRequest(new ChunkAllocationId(coords, 1))]);

        Assert.False(host.Release(coords));

        Assert.Equal(1, attempts);
        Assert.Equal(1, host.PendingCount);
        Assert.Same(chunk, authorityTerrain.GetChunkAtCoords(coords.X, coords.Y));
    }

    [Fact]
    public void ReleaseCancelsSnapshotWaitingForReplicaInstallation()
    {
        using var authorityTerrain = new Terrain();
        var host = CreateHost(authorityTerrain);
        var coords = new Point2(9, 10);
        host.Request([new ChunkContentRequest(new ChunkAllocationId(coords, 1))]);
        for (var i = 0; i < 5; i++)
        {
            host.Update(1);
        }

        Assert.True(host.Release(coords));

        Assert.Equal(0, host.DrainReceived(new List<ClientChunkSnapshot>()));
    }

    private static LocalChunkContentHost CreateHost(
        Terrain terrain,
        Func<TerrainChunk, bool>? prepareRelease = null)
    {
        var pipeline = new AuthoritativeChunkGenerationPipeline(
            new FakeGenerator(),
            _ => false,
            _ => false);
        return new LocalChunkContentHost(terrain, pipeline, prepareRelease);
    }

    private sealed class FakeGenerator : ITerrainContentsGenerator
    {
        public int OceanLevel => 64;
        public Vector3 FindCoarseSpawnPosition() => Vector3.Zero;
        public float CalculateOceanShoreDistance(float x, float z) => 0;
        public float CalculateHeight(float x, float z) => 0;
        public int CalculateTemperature(float x, float z) => 0;
        public int CalculateHumidity(float x, float z) => 0;
        public float CalculateMountainRangeFactor(float x, float z) => 0;
        public void GenerateChunkContentsPass1(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass2(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass3(TerrainChunk chunk) { }
        public void GenerateChunkContentsPass4(TerrainChunk chunk) => chunk.SetCellValueFast(0, 0, 0, 42);
    }
}
