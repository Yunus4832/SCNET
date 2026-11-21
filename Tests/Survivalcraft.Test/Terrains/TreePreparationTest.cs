using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Game.Subsystems;
using Game.Subsystems.Behavior;
using Game.Terrains;
using Game.Terrains.Generation;
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
        var cache = (System.Collections.IDictionary)typeof(TerrainContentsGenerator)
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

    [Theory]
    [InlineData(
        "game:standard",
        "continent",
        12345,
        13,
        9,
        "28CA8E0B4CB892E79CB620299FC0FC729F7490ED77A39E9A255F932990D1A4FF"
    )]
    [InlineData(
        "game:standard",
        "island",
        54321,
        -2,
        3,
        "E8ECE507D80ABD6953444E5D91B718D9A96CA3BA05962867CBF3B68528C2DF2D"
    )]
    [InlineData(
        "game:flat",
        "continent",
        12345,
        -13,
        -13,
        "84655D60CDBC041E9A09F8B0731B0ABEF21684ADF8595425151D045BB0CF05EE"
    )]
    [InlineData(
        "game:flat",
        "island",
        12345,
        12,
        12,
        "F3BF7830C0CA77C14F473E9EC96C625269E7DBC7AAA484F53B0CB8B0B4C1FF95"
    )]
    public async Task GeneratedChunksPreserveCurrentSeedOutput(
        string generatorId,
        string shape,
        int seed,
        int x,
        int z,
        string expected
    )
    {
        using var world = new GenerationWorld(seed, generatorId, shape);
        var chunk = world.GenerateBasis(x, z);
        await world.Generator.PrepareChunkContentsPass4Async(chunk, CancellationToken.None);
        world.Generator.GenerateChunkContentsPass4(chunk);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(MemoryMarshal.AsBytes(chunk.Cells.AsSpan()));
        hash.AppendData(MemoryMarshal.AsBytes(chunk.Shafts.AsSpan()));
        Assert.Equal(expected, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private sealed class GenerationWorld : IDisposable
    {
        private readonly WorldCatalogScope _scope = new();

        private readonly Terrain _terrain = new();

        private readonly SubsystemTerrain _world;

        public ITerrainContentsGenerator Generator { get; }

        public GenerationWorld(int seed, string generatorId = "game:standard", string shape = "continent")
        {
            _scope.Catalog.Blocks[0].Collidable = false;
            _scope.Catalog.Blocks[0].Transparent = true;
            _world = new SubsystemTerrain
            {
                Terrain = _terrain,
                SubsystemGameInfo = new SubsystemGameInfo
                {
                    WorldSeed = seed,
                    WorldSettings = new WorldSettings
                        { Generator = new TerrainGeneratorSettings(TerrainGeneratorDefinition.Find(generatorId)) }
                }
            };
            _world.SubsystemGameInfo.WorldSettings.Generator.Set("shape", shape);
            _scope.Project.InitializeSubsystem(_world);
            var support = new SubsystemBottomSuckerBlockBehavior();
            _scope.Project.AddSubsystem(support);
            Generator = _world.CreateTerrainContentsGenerator(_terrain);
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
