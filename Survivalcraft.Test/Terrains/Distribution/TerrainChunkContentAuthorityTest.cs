using Engine.Core;

using Game;
using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class TerrainChunkContentAuthorityTest
{
    [Fact]
    public void AuthorityPublishesOnlyLoadedContentAndTracksContentChanges()
    {
        var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(2, 3);
        var authority = new TerrainChunkContentAuthority(terrain);

        Assert.False(authority.TryGetSnapshot(chunk.Coords, out _));

        chunk.IsLoaded = true;
        chunk.WorkerState = TerrainChunkState.InvalidLight;
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
        chunk.IsLoaded = true;
        chunk.WorkerState = TerrainChunkState.InvalidLight;
        chunk.SetCellValueFast(0, 0, 0, 7);
        var authority = new TerrainChunkContentAuthority(terrain);

        Assert.True(authority.TryGetSnapshot(new Point2(2, 3), out var snapshot));
        chunk.SetCellValueFast(0, 0, 0, 9);

        Assert.Equal(7, snapshot.Cells.Span[0]);
    }
}
