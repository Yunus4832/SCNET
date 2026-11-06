using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Distribution;
using Game.TerrainSerializers;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class AuthoritativeChunkGenerationPipelineTest
{
    [Fact]
    public void ClosedGenerationCallbackCannotPublishReadyContent()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetWorkerState(TerrainChunkState.InvalidContents4);
        var pipeline = new AuthoritativeChunkGenerationPipeline(
            new FakeGenerator(),
            _ => false,
            _ => false,
            _ => false
        );

        Assert.False(pipeline.TryAdvance(chunk));
        Assert.False(chunk.IsLoaded);
        Assert.Equal(TerrainChunkState.InvalidContents4, chunk.WorkerState);
    }

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
            _ =>
            {
                generated++;
                return true;
            }
        );

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
            _ => throw new Xunit.Sdk.XunitException("Load must not run after pending-save restore.")
        );

        Assert.True(pipeline.TryAdvance(chunk));

        Assert.Equal(TerrainChunkState.InvalidLight, chunk.WorkerState);
        Assert.True(chunk.IsLoaded);
        Assert.Equal(0, generator.PassCount);
    }

    [Fact]
    public void DiskRestoreIsTriedOnlyAfterPendingSaveMisses()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var calls = new List<string>();
        var pipeline = new AuthoritativeChunkGenerationPipeline(
            new FakeGenerator(),
            _ =>
            {
                calls.Add("pending");
                return false;
            },
            _ =>
            {
                calls.Add("disk");
                return true;
            },
            _ => throw new InvalidOperationException("Restore does not run procedural generation callbacks.")
        );
        Assert.True(pipeline.TryAdvance(chunk));
        Assert.Equal(new[] { "pending", "disk" }, calls);
        Assert.True(chunk.IsLoaded);
        Assert.Equal(TerrainChunkState.InvalidLight, chunk.WorkerState);
    }

    [Fact]
    public void SeedBasisSkipsTheFirstThreePassesButStillFinalizesGeneratedContent()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var generator = new FakeGenerator { HasSeedBasis = true };
        var completed = 0;
        var pipeline = new AuthoritativeChunkGenerationPipeline(
            generator,
            _ => false,
            _ => false,
            _ =>
            {
                completed++;
                return true;
            }
        );
        for (var step = 0; step < 3; step++) { Assert.True(pipeline.TryAdvance(chunk)); }

        Assert.Equal(1, generator.PassCount);
        Assert.Equal(1, completed);
        Assert.True(chunk.IsLoaded);
        Assert.Equal(TerrainChunkState.InvalidLight, chunk.WorkerState);
    }

    [Theory]
    [InlineData(TerrainChunkState.InvalidLight)]
    [InlineData(TerrainChunkState.InvalidPropagatedLight)]
    [InlineData(TerrainChunkState.Ready)]
    public void LightingAndReadyStatesBelongToTheCpuDerivationPipeline(TerrainChunkState state)
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetWorkerState(state);
        var generator = new FakeGenerator();
        var pipeline = new AuthoritativeChunkGenerationPipeline(generator, _ => false, _ => false);
        Assert.False(pipeline.TryAdvance(chunk));
        Assert.Equal(state, chunk.WorkerState);
        Assert.Equal(0, generator.PassCount);
    }

    [Fact]
    public void FailedPassDoesNotAdvanceOrPublishLoadedContent()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetWorkerState(TerrainChunkState.InvalidContents2);
        var generator = new FakeGenerator { FailSecondPass = true };
        var pipeline = new AuthoritativeChunkGenerationPipeline(generator, _ => false, _ => false);
        Assert.Throws<InvalidOperationException>(() => pipeline.TryAdvance(chunk));
        Assert.Equal(TerrainChunkState.InvalidContents2, chunk.WorkerState);
        Assert.False(chunk.IsLoaded);
    }

    private sealed class FakeGenerator : ITerrainContentsGenerator
    {
        public int PassCount { get; private set; }

        public bool HasSeedBasis { get; init; }

        public bool FailSecondPass { get; init; }

        public bool TryTakeSeedGeneratedChunkBasis(TerrainChunk chunk) => HasSeedBasis;

        public int OceanLevel => 64;

        public Vector3 FindCoarseSpawnPosition() => Vector3.Zero;
        public float CalculateOceanShoreDistance(float x, float z) => 0;
        public float CalculateHeight(float x, float z) => 0;
        public int CalculateTemperature(float x, float z) => 0;
        public int CalculateHumidity(float x, float z) => 0;
        public float CalculateMountainRangeFactor(float x, float z) => 0;
        public void GenerateChunkContentsPass1(TerrainChunk chunk) => PassCount++;

        public void GenerateChunkContentsPass2(TerrainChunk chunk)
        {
            if (FailSecondPass) { throw new InvalidOperationException("Generation failed."); }

            PassCount++;
        }

        public void GenerateChunkContentsPass3(TerrainChunk chunk) => PassCount++;
        public void GenerateChunkContentsPass4(TerrainChunk chunk) => PassCount++;
    }
}
