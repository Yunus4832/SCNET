using Game.Terrains;
using Game.Terrains.Lighting;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainLightingStoreTest
{
    [Fact]
    public void EnvironmentChangesAffectQueriesWithoutInvalidatingSpatialPublications()
    {
        using var terrain = new Terrain();
        var chunk = Prepare(terrain);
        var input = terrain.Lighting.CaptureInput(chunk);
        var snapshot = new TerrainLightSnapshot(new TerrainLightChannels(15, 4));
        Assert.True(terrain.Lighting.TryPublish(chunk, input, snapshot));
        Assert.True(terrain.Lighting.TryGetEffectiveLight(0, 64, 0, new TerrainLightEnvironment(15), out var day));
        Assert.True(terrain.Lighting.TryGetEffectiveLight(0, 64, 0, new TerrainLightEnvironment(3), out var night));
        Assert.Equal(15, day);
        Assert.Equal(4, night);
        Assert.True(terrain.Lighting.TryGetCurrentSnapshot(chunk, out var current));
        Assert.Same(snapshot, current);
        Assert.Equal(input, terrain.Lighting.CaptureInput(chunk));
        Assert.Equal(0, chunk.GetCellValueFast(0, 64, 0));
    }

    [Fact]
    public void ContentChangesRejectOldJobsAndRetainThePreviousRenderPublication()
    {
        using var terrain = new Terrain();
        var chunk = Prepare(terrain);
        var input = terrain.Lighting.CaptureInput(chunk);
        var snapshot = new TerrainLightSnapshot(new TerrainLightChannels(15, 0));
        Assert.True(terrain.Lighting.TryPublish(chunk, input, snapshot));
        chunk.SetCellValueFast(0, 64, 0, Terrain.MakeBlockValue(1));
        Assert.False(terrain.Lighting.TryGetCurrentSnapshot(chunk, out _));
        Assert.False(terrain.Lighting.TryPublish(chunk, input, new TerrainLightSnapshot(default)));
        Assert.True(terrain.Lighting.TryGetPublishedSnapshot(chunk, out var previous));
        Assert.Same(snapshot, previous);
    }

    [Fact]
    public void RetiredAllocationsAndDisposedWorldsRejectPublicationAndQueries()
    {
        using var terrain = new Terrain();
        var chunk = Prepare(terrain);
        var input = terrain.Lighting.CaptureInput(chunk);
        var snapshot = new TerrainLightSnapshot(default);
        Assert.True(terrain.Lighting.TryPublish(chunk, input, snapshot));
        terrain.FreeChunk(chunk);
        var replacement = Prepare(terrain);
        Assert.False(terrain.Lighting.TryGetPublishedSnapshot(chunk, out _));
        Assert.False(terrain.Lighting.TryPublish(chunk, input, snapshot));
        Assert.False(terrain.Lighting.TryPublish(replacement, input, snapshot));
        Assert.False(terrain.Lighting.TryGetCurrentSnapshot(replacement, out _));
        terrain.Dispose();
        Assert.False(terrain.Lighting.TryGetEffectiveLight(0, 256, 0, new TerrainLightEnvironment(15), out _));
        Assert.False(terrain.Lighting.TryPublish(replacement, terrain.Lighting.CaptureInput(replacement), snapshot));
    }

    [Fact]
    public void MissingSpatialDataDoesNotMasqueradeAsDarknessAndForeignWorldsAreRejected()
    {
        using var terrain = new Terrain();
        using var other = new Terrain();
        var chunk = Prepare(terrain);
        Assert.False(terrain.Lighting.TryGetEffectiveLight(0, 64, 0, new TerrainLightEnvironment(15), out _));
        Assert.True(terrain.Lighting.TryGetEffectiveLight(0, 256, 0, new TerrainLightEnvironment(15), out var outside));
        Assert.Equal(0, outside);
        Assert.Throws<ArgumentException>(() => other.Lighting.CaptureInput(chunk));
    }

    private static TerrainChunk Prepare(Terrain terrain)
    {
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.HasContents = true;
        var request = chunk.DerivedRevisions.Capture();
        Assert.True(chunk.DerivedRevisions.PublishHeights(request));
        Assert.True(chunk.DerivedRevisions.PublishLightSeed(request));
        Assert.True(chunk.DerivedRevisions.PublishLight(request));
        return chunk;
    }
}
