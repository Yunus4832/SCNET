using Engine.Core;
using Game;
using Game.Terrains;
using Game.Terrains.Distribution;
using Game.TerrainSerializers;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class AuthoritativeChunkGenerationPipelineTest
{
    [Fact]
    public void GeneratedContentAdvancesOnExplicitAuthorityTerrain()
    {
        var authorityTerrain = new Terrain();
        var chunk = authorityTerrain.AllocateChunk(3, 4);
        var generator = new FakeGenerator();
        var generated = 0;
        var pipeline = new AuthoritativeChunkGenerationPipeline(
            generator,
            _ => false,
            _ => false,
            _ => generated++);

        for (var i = 0; i < 5; i++)
        {
            Assert.True(pipeline.TryAdvance(chunk));
        }

        Assert.Same(authorityTerrain, chunk.Terrain);
        Assert.Equal(TerrainChunkState.InvalidLight, chunk.WorkerState);
        Assert.True(chunk.IsLoaded);
        Assert.Equal(4, generator.PassCount);
        Assert.Equal(1, generated);
    }

    [Fact]
    public void RestoredContentSkipsProceduralGeneration()
    {
        var chunk = new Terrain().AllocateChunk(0, 0);
        var generator = new FakeGenerator();
        var pipeline = new AuthoritativeChunkGenerationPipeline(
            generator,
            _ => true,
            _ => throw new Xunit.Sdk.XunitException("Load must not run after pending-save restore."));

        Assert.True(pipeline.TryAdvance(chunk));

        Assert.Equal(TerrainChunkState.InvalidLight, chunk.WorkerState);
        Assert.True(chunk.IsLoaded);
        Assert.Equal(0, generator.PassCount);
    }

    private sealed class FakeGenerator : ITerrainContentsGenerator
    {
        public int PassCount { get; private set; }
        public int OceanLevel => 64;
        public Vector3 FindCoarseSpawnPosition() => Vector3.Zero;
        public float CalculateOceanShoreDistance(float x, float z) => 0;
        public float CalculateHeight(float x, float z) => 0;
        public int CalculateTemperature(float x, float z) => 0;
        public int CalculateHumidity(float x, float z) => 0;
        public float CalculateMountainRangeFactor(float x, float z) => 0;
        public void GenerateChunkContentsPass1(TerrainChunk chunk) => PassCount++;
        public void GenerateChunkContentsPass2(TerrainChunk chunk) => PassCount++;
        public void GenerateChunkContentsPass3(TerrainChunk chunk) => PassCount++;
        public void GenerateChunkContentsPass4(TerrainChunk chunk) => PassCount++;
    }
}
