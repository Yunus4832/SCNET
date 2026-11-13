using Game.Terrains;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainCellStorageTest
{
    [Fact]
    public void EmptyChunksReadAsAirWithoutAllocatingTheirCellArray()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        Assert.Equal(0, chunk.CellStorageBytes);
        Assert.Equal(0, chunk.GetCellValueFast(0));
        Assert.Equal(0, chunk.GetCellValueFast(15, 255, 15));
        Assert.Equal(0, chunk.GetCellValueForGeometry(100));
        chunk.SetCellValueFast(100, 0);
        Assert.Equal(0, chunk.CellStorageBytes);
        Assert.Equal(0, chunk.NetworkContentRevision);
        Assert.Throws<IndexOutOfRangeException>(() => chunk.GetCellValueFast(-1));
        Assert.Throws<IndexOutOfRangeException>(() => chunk.SetCellValueFast(65536, 0));
    }

    [Fact]
    public void FirstMutationAllocatesOneOwnedArrayAndPreservesContentRevisions()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetCellValueFast(3, 3);
        var cells = chunk.Cells;
        Assert.Equal(256 * 1024, chunk.CellStorageBytes);
        Assert.Equal(3, chunk.GetCellValueFast(3));
        Assert.Equal(1, chunk.NetworkContentRevision);
        chunk.SetCellValueFast(4, 4);
        Assert.Same(cells, chunk.Cells);
        Assert.Equal(2, chunk.NetworkContentRevision);
        chunk.SetCellValueFast(4, 4);
        Assert.Equal(2, chunk.NetworkContentRevision);
    }

    [Fact]
    public void InstallingOwnedContentsAvoidsAnUnusedDestinationAllocation()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        var contents = new int[65536];
        contents[123] = 3;
        Assert.Equal(0, chunk.CellStorageBytes);
        chunk.Cells = contents;
        Assert.Same(contents, chunk.Cells);
        Assert.Equal(3, chunk.GetCellValueFast(123));
        Assert.Equal(256 * 1024, chunk.CellStorageBytes);
    }

    [Fact]
    public void PredictionsOverEmptyContentsDoNotForceStorageAllocation()
    {
        using var terrain = new Terrain();
        var chunk = terrain.AllocateChunk(0, 0);
        chunk.SetGeometryPrediction(100, 0, 3);
        Assert.Equal(3, chunk.GetCellValueForGeometry(100));
        Assert.Equal(0, chunk.GetCellValueFast(100));
        Assert.Equal(0, chunk.CellStorageBytes);
    }
}
