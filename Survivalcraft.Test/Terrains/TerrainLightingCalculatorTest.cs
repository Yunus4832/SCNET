using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Distribution;
using Game.Terrains.Lighting;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class TerrainLightingCalculatorTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparationOrAdvanceFaultsReleaseTheSingleActivePropagation(bool duringAdvance)
    {
        using var world = new WorldCatalogScope();
        ConfigurePropagation(world);
        using var terrain = CreatePropagationTerrain();
        var source = terrain.GetChunkAtCoords(0, 0)!;
        PrepareEmitter(source);
        var fail = !duringAdvance;
        var calculator = new TerrainLightingCalculator(
            terrain,
            world.Catalog,
            false,
            _ =>
            {
                if (fail) { throw new InvalidOperationException("Simulated neighbor read failure."); }

                return true;
            }
        );
        if (duringAdvance)
        {
            calculator.BeginPropagation(source);
            fail = true;
            Assert.Throws<InvalidOperationException>(() => calculator.AdvancePropagation(source, 1));
        }
        else { Assert.Throws<InvalidOperationException>(() => calculator.BeginPropagation(source)); }

        Assert.False(calculator.HasActivePropagation);
        fail = false;
        calculator.ResetSunlightAndHeight(source, 0);
        calculator.BeginPropagation(source);
        Assert.True(calculator.HasActivePropagation);
        calculator.CancelPropagation();
        Assert.False(calculator.HasActivePropagation);
    }

    [Fact]
    public void BatchesRespectTheSourceBudgetAndOnlyReportTheirOwnChanges()
    {
        using var world = new WorldCatalogScope();
        ConfigurePropagation(world);
        using var terrain = CreatePropagationTerrain();
        var source = terrain.GetChunkAtCoords(0, 0)!;
        var right = terrain.GetChunkAtCoords(1, 0)!;
        PrepareEmitter(source);
        var calculator = new TerrainLightingCalculator(terrain, world.Catalog, false, _ => true);
        calculator.BeginPropagation(source);
        Assert.Same(source, Assert.Single(calculator.ChangedChunks));
        Assert.Equal(15, source.GetCellLightFast(15, 64, 8));
        Assert.Equal(0, source.GetCellLightFast(14, 64, 8));

        Assert.False(calculator.AdvancePropagation(source, 1));
        Assert.Equal(14, source.GetCellLightFast(14, 64, 8));
        Assert.Equal(0, source.GetCellLightFast(13, 64, 8));
        Assert.Contains(right, calculator.ChangedChunks);
        Assert.False(calculator.AdvancePropagation(source, 1));
        Assert.Equal(13, source.GetCellLightFast(13, 64, 8));
        Assert.DoesNotContain(right, calculator.ChangedChunks);
        Assert.False(calculator.AdvancePropagation(source, 1));
        Assert.Same(right, Assert.Single(calculator.ChangedChunks));
        Complete(calculator, source, 64);
        calculator.Propagate(source);
        Assert.Empty(calculator.ChangedChunks);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(4096)]
    public void PausedPropagationMatchesSynchronousCalculationWithoutRestarting(int budget)
    {
        using var world = new WorldCatalogScope();
        ConfigurePropagation(world);
        using var reference = CreatePropagationTerrain();
        using var paused = CreatePropagationTerrain();
        var referenceSource = reference.GetChunkAtCoords(0, 0)!;
        var pausedSource = paused.GetChunkAtCoords(0, 0)!;
        PrepareEmitter(referenceSource);
        PrepareEmitter(pausedSource);
        new TerrainLightingCalculator(reference, world.Catalog, false, _ => true).Propagate(referenceSource);
        var calculator = new TerrainLightingCalculator(paused, world.Catalog, false, _ => true);
        calculator.BeginPropagation(pausedSource);
        Complete(calculator, pausedSource, budget);

        foreach (var expected in reference.AllocatedChunks)
        {
            var actual = paused.GetChunkAtCoords(expected.Coords.X, expected.Coords.Y)!;
            Assert.Equal(expected.Cells, actual.Cells);
            Assert.Equal(expected.Shafts, actual.Shafts);
        }

        Assert.False(calculator.HasActivePropagation);
    }

    [Fact]
    public void HeightAndSunlightWorkOnAnotherRegionDoesNotClearAPausedSourceQueue()
    {
        using var world = new WorldCatalogScope();
        ConfigurePropagation(world);
        using var terrain = CreatePropagationTerrain();
        var source = terrain.GetChunkAtCoords(0, 0)!;
        PrepareEmitter(source);
        var calculator = new TerrainLightingCalculator(terrain, world.Catalog, false, _ => true);
        calculator.BeginPropagation(source);
        Assert.False(calculator.AdvancePropagation(source, 1));
        var unrelated = terrain.AllocateChunk(8, 8);
        unrelated.SetCellValueFast(1, 64, 1, 3);
        calculator.CalculateHeights(unrelated);
        calculator.ResetSunlight(unrelated, 0);
        Assert.True(calculator.HasActivePropagation);
        Assert.False(calculator.AdvancePropagation(source, 1));
        Assert.DoesNotContain(unrelated, calculator.ChangedChunks);
        Complete(calculator, source, 64);
        Assert.Equal(1, terrain.GetCellLightFast(29, 64, 8));
    }

    [Fact]
    public void WrongChunkAndCancellationDoNotMixOrResurrectPropagationTasks()
    {
        using var world = new WorldCatalogScope();
        ConfigurePropagation(world);
        using var terrain = CreatePropagationTerrain();
        var source = terrain.GetChunkAtCoords(0, 0)!;
        var other = terrain.GetChunkAtCoords(1, 0)!;
        PrepareEmitter(source);
        var calculator = new TerrainLightingCalculator(terrain, world.Catalog, false, _ => true);
        calculator.BeginPropagation(source);
        Assert.Throws<InvalidOperationException>(() => calculator.BeginPropagation(other));
        Assert.Throws<ArgumentException>(() => calculator.AdvancePropagation(other, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.AdvancePropagation(source, 0));
        Assert.True(calculator.HasActivePropagation);
        Assert.False(calculator.AdvancePropagation(source, 1));
        calculator.CancelPropagation();
        calculator.CancelPropagation();
        Assert.False(calculator.HasActivePropagation);
        Assert.Empty(calculator.ChangedChunks);
        Assert.Throws<InvalidOperationException>(() => calculator.AdvancePropagation(source, 1));
        calculator.BeginPropagation(other);
        calculator.CancelPropagation();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExpiredOrFreedAllocationsCannotAdvanceTheirOldSources(bool free)
    {
        using var world = new WorldCatalogScope();
        ConfigurePropagation(world);
        using var terrain = CreatePropagationTerrain();
        var source = terrain.GetChunkAtCoords(0, 0)!;
        PrepareEmitter(source);
        var calculator = new TerrainLightingCalculator(terrain, world.Catalog, false, _ => true);
        calculator.BeginPropagation(source);
        if (free)
        {
            terrain.FreeChunk(source);
            Assert.Throws<ObjectDisposedException>(() => calculator.AdvancePropagation(source, 1));
        }
        else
        {
            terrain.RenewChunkAllocation(source);
            Assert.Throws<InvalidOperationException>(() => calculator.AdvancePropagation(source, 1));
        }

        Assert.False(calculator.HasActivePropagation);
        Assert.Equal(0, terrain.GetCellLightFast(16, 64, 8));
    }

    [Fact]
    public void MoreThanTheOld120000SourcesContinueUntilTheFrontierReallyDrains()
    {
        using var world = new WorldCatalogScope();
        ConfigurePropagation(world);
        world.Catalog.Blocks[4].Transparent = true;
        world.Catalog.Blocks[4].LightAttenuation = 0;
        world.Catalog.Blocks[4].EmittedLightAmount = 15;
        using var terrain = CreatePropagationTerrain();
        var source = terrain.GetChunkAtCoords(0, 0)!;
        Array.Fill(source.Cells, 4);
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++) { source.SetTopHeightFast(x, z, 255); }
        }

        var calculator = new TerrainLightingCalculator(terrain, world.Catalog, false, _ => true);
        calculator.BeginPropagation(source);
        Assert.False(calculator.AdvancePropagation(source, 120000));
        Assert.True(calculator.HasActivePropagation);
        Complete(calculator, source, 8192);
        Assert.Equal(1, terrain.GetCellLightFast(29, 64, 8));
        Assert.Equal(0, terrain.GetCellLightFast(30, 64, 8));
        Assert.False(calculator.HasActivePropagation);
    }

    private static Terrain CreatePropagationTerrain()
    {
        var terrain = new Terrain();
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++) { terrain.AllocateChunk(x, z).HasContents = true; }
        }

        return terrain;
    }

    private static void ConfigurePropagation(WorldCatalogScope world)
    {
        world.Catalog.Blocks[0].Transparent = true;
        world.Catalog.Blocks[0].LightAttenuation = 0;
        world.Catalog.Blocks[0].EmittedLightAmount = 0;
        world.Catalog.Blocks[3].Transparent = false;
        world.Catalog.Blocks[3].LightAttenuation = 0;
        world.Catalog.Blocks[3].EmittedLightAmount = 15;
    }

    private static void PrepareEmitter(TerrainChunk chunk)
    {
        chunk.SetCellValueFast(15, 64, 8, 3);
        chunk.SetTopHeightFast(15, 8, 64);
    }

    private static void Complete(TerrainLightingCalculator calculator, TerrainChunk chunk, int budget)
    {
        var maximumBatches = Math.Max(128, 20000 / budget);
        for (var batches = 0; batches < maximumBatches; batches++)
        {
            if (calculator.AdvancePropagation(chunk, budget)) { return; }
        }

        Assert.Fail("The bounded light frontier did not converge.");
    }

    [Fact]
    public void CombinedOperationRetainsHeightOnlyChanges()
    {
        using var world = new WorldCatalogScope();
        world.Catalog.Blocks[0].Transparent = true;
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetTopHeightFast(0, 0, 64);
        for (var x = 0; x < 16; x++)
        {
            for (var z = 0; z < 16; z++) { chunk.SetSunlightHeightFast(x, z, 1); }
        }

        var calculator = new TerrainLightingCalculator(terrain, world.Catalog, false, _ => true);

        calculator.ResetSunlightAndHeight(chunk, 0);

        Assert.Equal(0, chunk.GetTopHeightFast(0, 0));
        Assert.Equal(1, chunk.GetSunlightHeightFast(0, 0));
        Assert.Same(chunk, Assert.Single(calculator.ChangedChunks));
    }

    [Fact]
    public void HeightCalculationCanCompleteWithoutChangingAnyLightValues()
    {
        using var world = new WorldCatalogScope();
        world.Catalog.Blocks[0].Transparent = true;
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(1, 64, 1, Terrain.MakeBlockValue(3, 7, 0));
        chunk.SetSunlightHeightFast(1, 1, 99);
        var cells = chunk.Cells.ToArray();
        var revision = chunk.NetworkContentRevision;
        var calculator = new TerrainLightingCalculator(terrain, world.Catalog, false, _ => true);

        calculator.CalculateHeights(chunk);

        Assert.Equal(64, chunk.GetTopHeightFast(1, 1));
        Assert.Equal(0, chunk.GetBottomHeightFast(1, 1));
        Assert.Equal(99, chunk.GetSunlightHeightFast(1, 1));
        Assert.Equal(cells, chunk.Cells);
        Assert.Equal(revision, chunk.NetworkContentRevision);
        Assert.Same(chunk, Assert.Single(calculator.ChangedChunks));

        calculator.CalculateHeights(chunk);
        Assert.Empty(calculator.ChangedChunks);
        calculator.ResetSunlight(chunk, 15);
        Assert.Equal(65, chunk.GetSunlightHeightFast(1, 1));
        Assert.Equal(0, chunk.GetCellLightFast(1, 64, 1));
        Assert.Equal(15, chunk.GetCellLightFast(1, 65, 1));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(7, false)]
    [InlineData(15, false)]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(15, true)]
    public void CombinedAndSeparateStagesProduceTheSameLightAndColumns(int skylight, bool includePredictions)
    {
        using var world = new WorldCatalogScope();
        world.Catalog.Blocks[0].Transparent = true;
        using var combinedTerrain = new Terrain();
        using var separateTerrain = new Terrain();
        using var staticTerrain = new Terrain();
        var combined = combinedTerrain.AllocateChunk(0, 0);
        var separate = separateTerrain.AllocateChunk(0, 0);
        var staticChunk = staticTerrain.AllocateChunk(0, 0);
        foreach (var chunk in new[] { combined, separate, staticChunk })
        {
            for (var x = 0; x < 16; x++)
            {
                for (var z = 0; z < 16; z++)
                {
                    var height = 40 + (x * 7 + z * 13) % 25;
                    for (var y = 0; y < 256; y++)
                    {
                        var solid = y == 0 || y == height || x % 3 == 0 && y < height;
                        chunk.SetCellValueFast(x, y, z, Terrain.MakeBlockValue(solid ? 3 : 0, (x + y + z) % 16, 0));
                    }
                }
            }

            var prediction = new ClientTerrainPrediction(chunk.Terrain, _ => { });
            prediction.Begin(new Point3(0, 40, 0), chunk.GetCellValueFast(0, 40, 0), 0, 0);
        }

        var combinedCalculator = new TerrainLightingCalculator(
            combinedTerrain,
            world.Catalog,
            includePredictions,
            _ => true
        );
        var separateCalculator = new TerrainLightingCalculator(
            separateTerrain,
            world.Catalog,
            includePredictions,
            _ => true
        );
        combinedCalculator.ResetSunlightAndHeight(combined, skylight);
        separateCalculator.CalculateHeights(separate);
        separateCalculator.ResetSunlight(separate, skylight);
        TerrainLightingCalculator.CalculateSunlightAndHeight(staticChunk, skylight, world.Catalog, includePredictions);

        Assert.Equal(combined.Cells, separate.Cells);
        Assert.Equal(combined.Shafts, separate.Shafts);
        Assert.Equal(combined.Cells, staticChunk.Cells);
        Assert.Equal(combined.Shafts, staticChunk.Shafts);
        Assert.Same(combined, Assert.Single(combinedCalculator.ChangedChunks));
    }

    [Fact]
    public void ReplicaLightNeverReadsOrWritesAnUninstalledNeighborAndTracksCrossChunkChanges()
    {
        using var world = new WorldCatalogScope();
        world.Catalog.Blocks[0].Transparent = true;
        world.Catalog.Blocks[0].LightAttenuation = 0;
        world.Catalog.Blocks[3].Transparent = true;
        world.Catalog.Blocks[3].LightAttenuation = 0;
        world.Catalog.Blocks[3].EmittedLightAmount = 15;
        using var terrain = new Terrain();
        for (var x = -1; x <= 1; x++)
        {
            for (var z = -1; z <= 1; z++) { terrain.AllocateChunk(x, z).HasContents = true; }
        }

        var source = terrain.GetChunkAtCoords(0, 0)!;
        var neighbor = terrain.GetChunkAtCoords(1, 0)!;
        neighbor.HasContents = false;
        neighbor.SetCellLightFast(TerrainChunk.CalculateCellIndex(0, 64, 0), 15);
        source.SetCellValueFast(15, 64, 8, 3);
        source.SetTopHeightFast(15, 8, 64);
        var sourceVersion = source.NetworkContentRevision;
        var neighborVersion = neighbor.NetworkContentRevision;
        var calculator = new TerrainLightingCalculator(terrain, world.Catalog, true, chunk => chunk.HasContents);

        calculator.Propagate(source);

        Assert.Equal(15, source.GetCellLightFast(15, 64, 8));
        Assert.Equal(0, neighbor.GetCellLightFast(0, 64, 8));
        Assert.Equal(7, source.GetCellLightFast(15, 64, 0));
        Assert.Equal(15, neighbor.GetCellLightFast(0, 64, 0));
        Assert.Contains(source, calculator.ChangedChunks);
        Assert.DoesNotContain(neighbor, calculator.ChangedChunks);

        neighbor.HasContents = true;
        calculator.ResetSunlightAndHeight(source, 0);
        calculator.Propagate(source);

        Assert.Equal(14, neighbor.GetCellLightFast(0, 64, 8));
        Assert.Contains(source, calculator.ChangedChunks);
        Assert.Contains(neighbor, calculator.ChangedChunks);
        Assert.Equal(sourceVersion, source.NetworkContentRevision);
        Assert.Equal(neighborVersion, neighbor.NetworkContentRevision);

        calculator.Propagate(source);
        Assert.Empty(calculator.ChangedChunks);
    }

    [Fact]
    public void ResetTracksColumnChangesSeparatelyFromContentVersions()
    {
        using var world = new WorldCatalogScope();
        world.Catalog.Blocks[0].Transparent = true;
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var contentVersion = chunk.NetworkContentRevision;
        var calculator = new TerrainLightingCalculator(terrain, world.Catalog, false, _ => true);

        calculator.ResetSunlightAndHeight(chunk, 15);
        Assert.Same(chunk, Assert.Single(calculator.ChangedChunks));
        Assert.Equal(contentVersion, chunk.NetworkContentRevision);

        calculator.ResetSunlightAndHeight(chunk, 15);
        Assert.Empty(calculator.ChangedChunks);
    }
}
