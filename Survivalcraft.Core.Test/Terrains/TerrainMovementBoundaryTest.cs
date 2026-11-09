using Engine.Core.Mathematics;

using Game.Terrains;

namespace Survivalcraft.Core.Test.Terrains;

public sealed class TerrainMovementBoundaryTest
{
    private static readonly Vector3 _size = new(0.6f, 1.8f, 0.6f);

    [Theory]
    [InlineData(15.2f, 1f, 0.5f)]
    [InlineData(0.8f, -1f, -0.5f)]
    [InlineData(15.7f, 1f, 0f)]
    [InlineData(0.3f, -1f, 0f)]
    public void UnknownEdgesStopTheLeadingFaceWithoutMovingTheBodyBackwards(float x, float move, float expected)
    {
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).HasContents = true;
        Assert.Equal(expected, TerrainMovementBoundary.ClipAxisMove(terrain, new Vector3(x, 70, 8), _size, 0, move), 4);
    }

    [Fact]
    public void AStoppedBodyCanRetreatAndMoveAlongTheKnownEdge()
    {
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).HasContents = true;
        var position = new Vector3(15.7f, 70, 8);
        Assert.Equal(-1f, TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 0, -1f));
        Assert.Equal(1f, TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 2, 1f));
        Assert.Equal(-1f, TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 1, -1f));
    }

    [Theory]
    [InlineData(15.9f, 1f)]
    [InlineData(0.1f, -1f)]
    public void AnAlreadyOverlappingBodyCanEscapeOrSlideWithoutIncreasingUnknownPenetration(float x, float direction)
    {
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).HasContents = true;
        var position = new Vector3(x, 70, 8);
        Assert.Equal(0f, TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 0, direction));
        Assert.Equal(-direction, TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 0, -direction));
        Assert.Equal(1f, TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 2, 1f));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheEdgeOpensForPublishedContentsWithoutWaitingForLightingOrMeshes(bool available)
    {
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).HasContents = true;
        var neighbor = terrain.AllocateChunk(1, 0);
        neighbor.HasContents = available;
        Assert.False(neighbor.HasCurrentHeightData);
        Assert.False(neighbor.HasCurrentLight);
        var move = TerrainMovementBoundary.ClipAxisMove(terrain, new Vector3(15.7f, 70, 8), _size, 0, 1f);
        Assert.Equal(available ? 1f : 0f, move, 4);
    }

    [Fact]
    public void LongMovesCannotSkipAnUnknownChunkToReachAKnownChunk()
    {
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).HasContents = true;
        terrain.AllocateChunk(2, 0).HasContents = true;
        Assert.Equal(7.7f, TerrainMovementBoundary.ClipAxisMove(terrain, new Vector3(8, 70, 8), _size, 0, 32f), 4);
    }

    [Fact]
    public void NegativeCoordinatesAndAChunkCornerUseTheSameBoundaryRule()
    {
        using var terrain = new Terrain();
        terrain.AllocateChunk(-1, -1).HasContents = true;
        var position = new Vector3(-15.7f, 70, -8);
        Assert.Equal(0f, TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 0, -1f), 4);
        Assert.Equal(1f, TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 0, 1f));
        terrain.AllocateChunk(0, 0).HasContents = true;
        terrain.AllocateChunk(1, 0).HasContents = true;
        terrain.AllocateChunk(0, 1).HasContents = true;
        position = new Vector3(15.7f, 70, 15.7f);
        var x = TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 0, 1f);
        position.X += x;
        Assert.Equal(1f, x);
        Assert.Equal(0f, TerrainMovementBoundary.ClipAxisMove(terrain, position, _size, 2, 1f), 4);
    }
}
