using Engine.Core.Mathematics;

using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Core.Test.Terrains.Distribution;

public sealed class TerrainChunkContentAuthorityTest
{
    [Fact]
    public void PublishedCollisionContentsCannotEscapeAsABaselineBeforeInitialBusinessNormalization()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var authority = new TerrainChunkContentAuthority(terrain);
        chunk.HasContents = true;
        chunk.AreBehaviorsNotified = true;
        chunk.SetCellValueFast(1, 64, 1, 7);
        Assert.False(authority.TryGetDescriptor(chunk.Coords, out _));
        Assert.False(authority.TryGetSnapshot(chunk.Coords, out _));
        chunk.SetCellValueFast(1, 64, 1, 8);
        chunk.IsContentInitialized = true;
        Assert.True(authority.TryGetSnapshot(chunk.Coords, out var baseline));
        Assert.Equal(8, baseline.Cells.Span[TerrainChunk.CalculateCellIndex(1, 64, 1)]);
        Assert.Equal(chunk.NetworkContentRevision + 1, baseline.ContentVersion);
    }

    [Fact]
    public void AuthorityPublishesOnlyLoadedContentAndTracksContentChanges()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(2, 3);
        var authority = new TerrainChunkContentAuthority(terrain);

        Assert.False(authority.TryGetSnapshot(chunk.Coords, out _));

        chunk.HasContents = true;

        chunk.IsContentInitialized = true;

        chunk.SetCellValueFast(0, 0, 0, 7);
        Assert.True(authority.TryGetDescriptor(chunk.Coords, out var descriptor));
        Assert.True(authority.TryGetSnapshot(chunk.Coords, out var first));
        Assert.Equal(first.ContentVersion, descriptor.ContentVersion);
        Assert.Equal(7, first.Cells.Span[0]);

        chunk.SetCellValueFast(0, 0, 0, 8);
        Assert.True(authority.TryGetSnapshot(chunk.Coords, out var second));
        Assert.True(second.ContentVersion > first.ContentVersion);
        Assert.Equal(8, second.Cells.Span[0]);
    }

    [Fact]
    public void AuthoritySnapshotDoesNotExposeMutableTerrainArrays()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(2, 3);
        chunk.HasContents = true;
        chunk.IsContentInitialized = true;

        chunk.SetCellValueFast(0, 0, 0, 7);
        var authority = new TerrainChunkContentAuthority(terrain);

        Assert.True(authority.TryGetSnapshot(new Point2(2, 3), out var snapshot));
        chunk.SetCellValueFast(0, 0, 0, 9);

        Assert.Equal(7, snapshot.Cells.Span[0]);
    }
}
