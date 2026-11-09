using Game.TerrainSerializers;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class SeedTerrainGenerationPolicyTest
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(4, 3)]
    [InlineData(8, 4)]
    public void SeedTerrainParallelismLeavesCapacityForOtherWork(int processorCount, int expected)
    {
        Assert.Equal(expected, SeedTerrainGenerationPolicy.GetParallelism(processorCount));
    }
}
