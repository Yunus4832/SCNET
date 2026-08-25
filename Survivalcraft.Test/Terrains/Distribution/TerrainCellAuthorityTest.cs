using Game.Terrains;
using Game.Terrains.Distribution;

namespace Survivalcraft.Test.Terrains.Distribution;

public sealed class TerrainCellAuthorityTest
{
    [Fact]
    public void ChangeCellUpdatesContentRevisionAndModificationCounter()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(2, -1);
        var authority = new TerrainCellAuthority(terrain);
        var revision = chunk.NetworkContentRevision;

        Assert.True(authority.ChangeCell(33, 7, -15, Terrain.MakeBlockValue(2, 15, 3)));

        Assert.Equal(Terrain.MakeBlockValue(2, 0, 3), authority.GetCellValue(33, 7, -15));
        Assert.Equal(revision + 1, chunk.NetworkContentRevision);
        Assert.Equal(1, chunk.ModificationCounter);
    }

    [Fact]
    public void RepeatedValueAndMissingAllocationAreIgnored()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var authority = new TerrainCellAuthority(terrain);

        Assert.True(authority.ChangeCell(1, 2, 3, 42, false));
        var revision = chunk.NetworkContentRevision;

        Assert.False(authority.ChangeCell(1, 2, 3, Terrain.ReplaceLight(42, 15), false));
        Assert.False(authority.ChangeCell(32, 2, 32, 42));
        Assert.Equal(revision, chunk.NetworkContentRevision);
        Assert.Equal(0, chunk.ModificationCounter);
    }
}
