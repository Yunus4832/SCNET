using Game.Terrains;
using Game.Terrains.Lighting;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Terrains;

[Collection(ConfigFileCollection.Name)]
public sealed class TerrainLightingCalculatorTest
{
    [Fact]
    public void RestoringTheSameComputedValuesStillNotifiesConsumersOfAChangedSeedBoundary()
    {
        using var world = new WorldCatalogScope();
        Configure(world);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        var calculator = Create(terrain, world);
        Prepare(calculator, chunk);
        var original = new TerrainLightSnapshot(new TerrainLightChannels(15, 7));
        Assert.True(terrain.Lighting.TryPublish(chunk, terrain.Lighting.CaptureInput(chunk), original));
        chunk.SetCellValueFast(8, 64, 8, Terrain.MakeBlockValue(1));
        chunk.DerivedRevisions.InvalidateHeightsAndLight();
        Prepare(calculator, chunk);
        Assert.True(terrain.Lighting.TryGetPropagationSnapshot(chunk, out var seedInput));
        Assert.Equal(0, seedInput.Get(0, 64, 0).Block);
        Assert.True(calculator.TryCapturePropagation(chunk, out var job));
        var publication = calculator.Publish(job, original);
        Assert.True(publication.Published);
        Assert.False(publication.ValuesChanged);
        Assert.Equal(15, publication.ChangedBoundaryFaces);
    }

    [Fact]
    public void DirectInputsKeepSkyAndEmissionSeparateAndLeaveContentsUnchanged()
    {
        using var world = new WorldCatalogScope();
        Configure(world);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.SetCellValueFast(8, 64, 8, Terrain.MakeBlockValue(3));
        var before = (int[])chunk.Cells.Clone();
        var calculator = Create(terrain, world);
        Prepare(calculator, chunk);
        Assert.True(terrain.Lighting.TryGetSeed(chunk, out var seed));
        Assert.Equal(new TerrainLightChannels(15, 15), seed.Light.Get(8, 64, 8));
        Assert.Equal(new TerrainLightChannels(15, 0), seed.Light.Get(7, 64, 8));
        Assert.Equal(before, chunk.Cells);
        Assert.True(terrain.Lighting.TryGetSeed(chunk, out var same));
        Assert.Same(seed, same);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(2048)]
    public void BoundedPrivatePropagationMatchesSynchronousPublication(int budget)
    {
        using var world = new WorldCatalogScope();
        Configure(world);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.SetCellValueFast(8, 64, 8, Terrain.MakeBlockValue(3));
        var calculator = Create(terrain, world);
        Prepare(calculator, chunk);
        var before = (int[])chunk.Cells.Clone();
        Assert.True(calculator.TryCapturePropagation(chunk, out var job));
        using var work = job.CreateComputation();
        Assert.False(work.Advance(1));
        Assert.False(terrain.Lighting.TryGetPublishedSnapshot(chunk, out _));
        var complete = false;
        for (var i = 0; i < 100000 && !complete; i++) { complete = work.Advance(budget); }

        Assert.True(complete);
        Assert.True(calculator.Publish(job, work.Capture()).Published);
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(chunk, out var result));
        Assert.Equal(14, result.Get(7, 64, 8).Block);
        Assert.Equal(13, result.Get(7, 64, 7).Block);
        Assert.Equal(before, chunk.Cells);
        var repeated = calculator.Propagate(chunk);
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(chunk, out var synchronous));
        Assert.True(result.HasSameValues(synchronous));
        Assert.False(repeated.ValuesChanged);
        Assert.Equal(0, repeated.ChangedBoundaryFaces);
    }

    [Fact]
    public void NeighborPublicationsConvergeAcrossAnEdgeAndDecayAfterSourceRemoval()
    {
        using var world = new WorldCatalogScope();
        Configure(world);
        using var terrain = new Terrain();
        var left = terrain.AllocateChunk(0, 0);
        var right = terrain.AllocateChunk(1, 0);
        left.HasContents = right.HasContents = true;
        left.SetCellValueFast(15, 64, 8, Terrain.MakeBlockValue(3));
        var calculator = Create(terrain, world);
        Prepare(calculator, left);
        Prepare(calculator, right);
        calculator.Propagate(left);
        Assert.False(terrain.Lighting.TryGetPublishedSnapshot(right, out _));
        Assert.Equal(0, right.GetCellValueFast(0, 64, 8));
        calculator.Propagate(right);
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(right, out var illuminated));
        Assert.Equal(14, illuminated.Get(0, 64, 8).Block);
        left.SetCellValueFast(15, 64, 8, 0);
        left.DerivedRevisions.InvalidateHeightsAndLight();
        Prepare(calculator, left);
        for (var i = 0; i < 16; i++)
        {
            calculator.Propagate(left);
            calculator.Propagate(right);
        }

        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(left, out var darkLeft));
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(right, out var darkRight));
        Assert.Equal(0, darkLeft.Get(15, 64, 8).Block);
        Assert.Equal(0, darkRight.Get(0, 64, 8).Block);
        Assert.Equal(14, illuminated.Get(0, 64, 8).Block);
    }

    [Fact]
    public void CancelledAndStaleTasksCannotReplaceAPublication()
    {
        using var world = new WorldCatalogScope();
        Configure(world);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.SetCellValueFast(8, 64, 8, Terrain.MakeBlockValue(3));
        var calculator = Create(terrain, world);
        Prepare(calculator, chunk);
        calculator.Propagate(chunk);
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(chunk, out var original));
        Assert.True(calculator.TryCapturePropagation(chunk, out var job));
        using var work = job.CreateComputation();
        Assert.False(work.Advance(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => work.Advance(2048, cancellation.Token));
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(chunk, out var afterCancel));
        Assert.Same(original, afterCancel);
        Assert.True(calculator.TryCapturePropagation(chunk, out var stale));
        var calculated = stale.Compute();
        chunk.SetCellValueFast(8, 64, 8, 0);
        Assert.False(calculator.Publish(stale, calculated).Published);
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(chunk, out var afterStale));
        Assert.Same(original, afterStale);
    }

    [Fact]
    public void BoundaryChangeDuringAJobRejectsItsPrivateResult()
    {
        using var world = new WorldCatalogScope();
        Configure(world);
        using var terrain = new Terrain();
        var left = terrain.AllocateChunk(0, 0);
        var right = terrain.AllocateChunk(1, 0);
        left.HasContents = right.HasContents = true;
        left.SetCellValueFast(15, 64, 8, Terrain.MakeBlockValue(3));
        var first = Create(terrain, world);
        var second = Create(terrain, world);
        Prepare(first, left);
        Prepare(first, right);
        Assert.True(first.TryCapturePropagation(right, out var rightJob));
        second.Propagate(left);
        Assert.False(first.Publish(rightJob, rightJob.Compute()).Published);
        Assert.False(terrain.Lighting.TryGetPublishedSnapshot(right, out _));
        Assert.True(first.Propagate(right).Published);
    }

    [Fact]
    public void PredictionsAffectOnlyReplicaLightInputs()
    {
        using var world = new WorldCatalogScope();
        Configure(world);
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.SetGeometryPrediction(TerrainChunk.CalculateCellIndex(8, 64, 8), 0, Terrain.MakeBlockValue(3));
        var authority = Create(terrain, world);
        var replica = Create(terrain, world, true);
        Prepare(authority, chunk);
        Assert.True(terrain.Lighting.TryGetSeed(chunk, out var actual));
        Assert.Equal(0, actual.Light.Get(8, 64, 8).Block);
        Prepare(replica, chunk);
        Assert.True(terrain.Lighting.TryGetSeed(chunk, out var predicted));
        Assert.Equal(15, predicted.Light.Get(8, 64, 8).Block);
        Assert.Equal(0, chunk.GetCellValueFast(8, 64, 8));
    }

    [Fact]
    public void OpaqueAndAbsorbingCellsKeepIndependentDirectSkyInputs()
    {
        using var world = new WorldCatalogScope();
        Configure(world);
        world.Catalog.Blocks[1].Transparent = false;
        world.Catalog.Blocks[2].Transparent = true;
        world.Catalog.Blocks[2].LightAttenuation = 2;
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        chunk.SetCellValueFast(1, 70, 1, Terrain.MakeBlockValue(1));
        chunk.SetCellValueFast(2, 70, 2, Terrain.MakeBlockValue(2));
        Prepare(Create(terrain, world), chunk);
        Assert.True(terrain.Lighting.TryGetSeed(chunk, out var seed));
        Assert.Equal(15, seed.Light.Get(1, 71, 1).Sky);
        Assert.Equal(0, seed.Light.Get(1, 70, 1).Sky);
        Assert.Equal(0, seed.Light.Get(1, 69, 1).Sky);
        Assert.Equal(13, seed.Light.Get(2, 70, 2).Sky);
        Assert.Equal(13, seed.Light.Get(2, 69, 2).Sky);
    }

    private static void Prepare(TerrainLightingCalculator calculator, TerrainChunk chunk)
    {
        var request = chunk.DerivedRevisions.Capture();
        calculator.CalculateHeights(chunk);
        Assert.True(chunk.DerivedRevisions.PublishHeights(request));
        Assert.True(calculator.CalculateLightSeed(chunk).Published);
        Assert.True(chunk.DerivedRevisions.PublishLightSeed(request));
    }

    private static TerrainLightingCalculator Create(Terrain terrain, WorldCatalogScope world, bool predictions = false)
    {
        return new TerrainLightingCalculator(
            terrain,
            world.Catalog,
            predictions,
            chunk => chunk.HasContents && !chunk.NeedsContentValidation
        );
    }

    private static void Configure(WorldCatalogScope world)
    {
        world.Catalog.Blocks[0].Transparent = true;
        world.Catalog.Blocks[0].LightAttenuation = 0;
        world.Catalog.Blocks[3].Transparent = true;
        world.Catalog.Blocks[3].LightAttenuation = 0;
        world.Catalog.Blocks[3].EmittedLightAmount = 15;
    }
}
