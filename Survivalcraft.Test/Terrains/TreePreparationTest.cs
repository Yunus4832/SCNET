using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.TerrainSerializers;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class TreePreparationTest
{
    [Theory]
    [InlineData(12345, 0, 0, false)]
    [InlineData(54321, -2, 3, false)]
    [InlineData(12345, 13, 9, false)]
    [InlineData(12345, 13, 9, true)]
    public async Task PreparedTreesPreserveAllGeneratedCellsAndShafts(int seed, int x, int z, bool evictPreparedCache)
    {
        using var original = new GenerationWorld(seed);
        using var prepared = new GenerationWorld(seed);
        var expected = original.GenerateBasis(x, z);
        await original.Generator.PrepareChunkContentsPass4Async(expected, CancellationToken.None);
        original.Generator.GenerateChunkContentsPass4(expected);
        var neighbor = prepared.GenerateBasis(x - 1, z);
        await prepared.Generator.PrepareChunkContentsPass4Async(neighbor, CancellationToken.None);
        prepared.Generator.GenerateChunkContentsPass4(neighbor);
        var actual = prepared.Allocate(x, z);
        Assert.True(prepared.Generator.TryTakeSeedGeneratedChunkBasis(actual));
        await prepared.Generator.PrepareChunkContentsPass4Async(actual, CancellationToken.None);
        var cache = (System.Collections.IDictionary)typeof(TerrainContentsGenerator24)
            .GetField(
                "_cleanTreeBrushCache",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .GetValue(prepared.Generator)!;
        if (evictPreparedCache) { cache.Clear(); }

        prepared.Generator.GenerateChunkContentsPass4(actual);
        if (evictPreparedCache) { Assert.Equal(1, cache.Count); }

        Assert.Equal(expected.Cells, actual.Cells);
        Assert.Equal(expected.Shafts, actual.Shafts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrStalePreparationIsRejectedBeforeChangingContents(bool prepareThenRenew)
    {
        using var world = new GenerationWorld(12345);
        var chunk = world.GenerateBasis(0, 0);
        if (prepareThenRenew)
        {
            await world.Generator.PrepareChunkContentsPass4Async(chunk, CancellationToken.None);
            chunk.Terrain.RenewChunkAllocation(chunk);
        }

        var cells = chunk.Cells.ToArray();
        var shafts = chunk.Shafts.ToArray();
        Assert.Throws<InvalidOperationException>(() => world.Generator.GenerateChunkContentsPass4(chunk));
        Assert.Equal(cells, chunk.Cells);
        Assert.Equal(shafts, chunk.Shafts);
    }

    [Fact]
    public async Task CompletedPreparationsRemainAvailableWhenAnotherChunkIsPreparedFirst()
    {
        using var world = new GenerationWorld(12345);
        var first = world.GenerateBasis(0, 0);
        var second = world.GenerateBasis(1, 0);
        await world.Generator.PrepareChunkContentsPass4Async(first, CancellationToken.None);
        await world.Generator.PrepareChunkContentsPass4Async(second, CancellationToken.None);
        world.Generator.GenerateChunkContentsPass4(second);
        world.Generator.GenerateChunkContentsPass4(first);
        using var reference = new GenerationWorld(12345);
        var expectedFirst = reference.GenerateBasis(0, 0);
        await reference.Generator.PrepareChunkContentsPass4Async(expectedFirst, CancellationToken.None);
        reference.Generator.GenerateChunkContentsPass4(expectedFirst);
        var expectedSecond = reference.GenerateBasis(1, 0);
        await reference.Generator.PrepareChunkContentsPass4Async(expectedSecond, CancellationToken.None);
        reference.Generator.GenerateChunkContentsPass4(expectedSecond);
        Assert.Equal(expectedFirst.Cells, first.Cells);
        Assert.Equal(expectedFirst.Shafts, first.Shafts);
        Assert.Equal(expectedSecond.Cells, second.Cells);
        Assert.Equal(expectedSecond.Shafts, second.Shafts);
    }

    private sealed class GenerationWorld : IDisposable
    {
        private readonly WorldCatalogScope _scope = new();

        private readonly Terrain _terrain = new();

        private readonly SubsystemTerrain _world;

        public TerrainContentsGenerator24 Generator { get; }

        public GenerationWorld(int seed)
        {
            _scope.Catalog.Blocks[0].Collidable = false;
            _scope.Catalog.Blocks[0].Transparent = true;
            _world = new SubsystemTerrain
            {
                Terrain = _terrain,
                SubsystemGameInfo = new SubsystemGameInfo { WorldSeed = seed, WorldSettings = new WorldSettings() }
            };
            _scope.Project.InitializeSubsystem(_world);
            var support = new SubsystemBottomSuckerBlockBehavior();
            _scope.Project.AddSubsystem(support);
            Generator = new TerrainContentsGenerator24(_world);
        }

        public TerrainChunk Allocate(int x, int z) => _terrain.AllocateChunk(x, z);

        public TerrainChunk GenerateBasis(int x, int z)
        {
            var chunk = Allocate(x, z);
            Generator.GenerateChunkContentsPass1(chunk);
            Generator.GenerateChunkContentsPass2(chunk);
            Generator.GenerateChunkContentsPass3(chunk);
            return chunk;
        }

        public void Dispose()
        {
            _world.WorkExecutor.Dispose();
            _terrain.Dispose();
            _scope.Dispose();
        }
    }
}
