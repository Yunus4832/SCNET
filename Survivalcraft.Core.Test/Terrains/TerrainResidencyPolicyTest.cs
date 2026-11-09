using Engine.Core.Mathematics;

using Game.Network.Chunks;
using Game.Protocol.Terrain;
using Game.Terrains;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainResidencyPolicyTest
{
    [Fact]
    public void ClientRetentionMarginCreatesAllocationHysteresis()
    {
        var locations = new[]
        {
            new TerrainUpdateLocation
            {
                Center = Vector2.Zero,
                VisibilityDistance = 128,
                ContentDistance = 128
            }
        };
        var bufferedCenter = new Vector2(152, 0);

        Assert.False(TerrainResidencyManager.IsChunkInRange(bufferedCenter, locations));
        Assert.True(
            TerrainResidencyManager.IsChunkInRange(
                bufferedCenter,
                locations,
                NetworkTerrainPolicy.ClientChunkRetentionMargin
            )
        );
        Assert.False(
            TerrainResidencyManager.IsChunkInRange(
                new Vector2(161, 0),
                locations,
                NetworkTerrainPolicy.ClientChunkRetentionMargin
            )
        );
    }

    [Fact]
    public void RetentionCapacityEvictsLeastRecentlyUsedChunks()
    {
        var oldest = new Point2(1, 0);
        var middle = new Point2(2, 0);
        var newest = new Point2(3, 0);

        var evicted = TerrainResidencyManager.SelectRetainedChunkCoordsToEvict(
            [
                (middle, 20),
                (oldest, 10),
                (newest, 30)
            ],
            2
        );

        Assert.Equal([oldest], evicted);
    }

    [Fact]
    public void ZeroRetentionCapacityEvictsEveryBufferedChunk()
    {
        var evicted = TerrainResidencyManager.SelectRetainedChunkCoordsToEvict(
            [
                (new Point2(1, 0), 10),
                (new Point2(2, 0), 20)
            ],
            0
        );

        Assert.Equal(2, evicted.Count);
    }
}
