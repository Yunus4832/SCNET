using Game.Terrains;
using Game.TerrainSerializers;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class SeedGeneratedChunkBasisTest
{
    [Fact]
    public void SeedGeneratedBasisMovesArraysOnlyOnce()
    {
        var cells = new int[16 * 16 * 256];
        var shafts = new long[16 * 16];
        cells[123] = 456;
        shafts[12] = 789;
        var basis = new SeedGeneratedChunkBasis(cells, shafts);
        var first = new TerrainChunk(null!, 1, 2);
        var second = new TerrainChunk(null!, 1, 2);

        Assert.True(basis.TryMoveTo(first));
        Assert.Same(cells, first.Cells);
        Assert.Same(shafts, first.Shafts);
        Assert.Equal(456, first.Cells[123]);
        Assert.Equal(789, first.Shafts[12]);
        Assert.False(basis.TryMoveTo(second));
        Assert.NotSame(cells, second.Cells);
    }
}
