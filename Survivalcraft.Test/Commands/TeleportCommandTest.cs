using Engine.Core;

using Game;
using Game.Commands;
using Game.Components;
using Game.Terrains;

namespace Survivalcraft.Test.Commands;

public class TeleportCommandTest
{
    [Fact]
    public void PreviousPointRecordsTheActualDepartureAndSupportsRoundTrips()
    {
        var a = new Vector3(10f, 65f, 10f);
        var b = new Vector3(100f, 65f, 100f);
        var body = new ComponentBody { Position = a };
        var marks = new PositionMarks();
        marks.Set("home", b);
        marks.Set("previous", TeleportCommandHandlers.ApplyPosition(body, b));
        Assert.True(marks.TryGet("previous", out var previous));
        Assert.Equal(a, previous);
        Assert.Equal(b, body.Position);
        marks.Set("previous", TeleportCommandHandlers.ApplyPosition(body, previous));
        Assert.True(marks.TryGet("previous", out previous));
        Assert.Equal(b, previous);
        Assert.Equal(a, body.Position);
        Assert.True(marks.TryGet("home", out var home));
        Assert.Equal(b, home);
        marks.Set("previous", TeleportCommandHandlers.ApplyPosition(body, previous));
        Assert.True(marks.TryGet("previous", out previous));
        Assert.Equal(a, previous);
        Assert.Equal(b, body.Position);
    }

    [Theory]
    [InlineData(29f, 30f, false)]
    [InlineData(30f, 31f, true)]
    [InlineData(31f, 32f, true)]
    [InlineData(29f, 29.5f, false)]
    [InlineData(29f, 30.005f, false)]
    [InlineData(32f, 33f, false)]
    public void DestinationChecksFeetAndHeadWithoutRejectingGroundContact(float bottom, float top, bool blocked)
    {
        var box = TeleportCommandHandlers.GetDestinationBox(new Vector3(30f), new Vector3(0.6f, 1.8f, 0.6f));
        DynamicArray<ComponentBody.CollisionBox> collisions = [];
        collisions.Add(new ComponentBody.CollisionBox
        {
            Box = new BoundingBox(new Vector3(29f, bottom, 29f), new Vector3(31f, top, 31f))
        });
        Assert.Equal(blocked, new ComponentBody().IsColliding(box, collisions));
    }

    [Fact]
    public void DestinationChecksAllOverlappingChunksIncludingNegativeCoordinates()
    {
        using var terrain = new Terrain();
        var box = TeleportCommandHandlers.GetDestinationBox(new Vector3(0f, 30f, 0f), new Vector3(0.6f, 1.8f, 0.6f));
        Assert.False(TeleportCommandHandlers.IsDestinationTerrainReady(terrain, box));
        for (var x = -1; x <= 0; x++)
        {
            for (var z = -1; z <= 0; z++)
            {
                terrain.AllocateChunk(x, z).MainThreadState = TerrainChunkState.InvalidLight;
            }
        }

        Assert.True(TeleportCommandHandlers.IsDestinationTerrainReady(terrain, box));
        terrain.GetChunkAtCoords(-1, -1)!.MainThreadState = TerrainChunkState.InvalidContents4;
        Assert.False(TeleportCommandHandlers.IsDestinationTerrainReady(terrain, box));
    }

    [Theory]
    [InlineData(TerrainChunkState.NotLoaded, false)]
    [InlineData(TerrainChunkState.InvalidContents4, false)]
    [InlineData(TerrainChunkState.InvalidLight, true)]
    [InlineData(TerrainChunkState.InvalidPropagatedLight, true)]
    [InlineData(TerrainChunkState.Valid, true)]
    public void SafetyCheckWaitsForContentsButNotLightingOrGeometry(TerrainChunkState state, bool ready)
    {
        using var terrain = new Terrain();
        terrain.AllocateChunk(0, 0).MainThreadState = state;
        var box = TeleportCommandHandlers.GetDestinationBox(new Vector3(8f, 30f, 8f), new Vector3(0.6f, 1.8f, 0.6f));
        Assert.Equal(ready, TeleportCommandHandlers.IsDestinationTerrainReady(terrain, box));
    }

    [Theory]
    [InlineData(30.2f, true)]
    [InlineData(30.5f, false)]
    public void DestinationChecksBodyWidthNotOnlyTheFootCell(float wallX, bool blocked)
    {
        var box = TeleportCommandHandlers.GetDestinationBox(new Vector3(30f), new Vector3(0.6f, 1.8f, 0.6f));
        DynamicArray<ComponentBody.CollisionBox> collisions = [];
        Assert.False(new ComponentBody().IsColliding(box, collisions));
        collisions.Add(new ComponentBody.CollisionBox
        {
            Box = new BoundingBox(new Vector3(wallX, 30f, 29f), new Vector3(wallX + 1f, 32f, 31f))
        });
        Assert.Equal(blocked, new ComponentBody().IsColliding(box, collisions));
    }

    [Fact]
    public void AuthoritativePositionResetsMotionAndInterpolation()
    {
        var body = new ComponentBody { Velocity = new Vector3(0f, -20f, 0f), CollisionVelocityChange = new Vector3(0f, 20f, 0f) };
        var target = new Vector3(100f, 65f, 200f);
        body.ApplyAuthoritativePosition(target, Vector3.Zero);
        Assert.Equal(target, body.Position);
        Assert.Equal(Vector3.Zero, body.Velocity);
        Assert.Equal(Vector3.Zero, body.CollisionVelocityChange);
        Assert.Equal(target, body.NetPosition.Get(0.1f));
        Assert.Equal(Vector3.Zero, body.NetVelocity.Get(0.1f));
    }

    [Theory]
    [InlineData(0f, 64f, 0f, true)]
    [InlineData(-1000000f, 0f, 1000000f, true)]
    [InlineData(0f, 255f, 0f, true)]
    [InlineData(0f, -1f, 0f, false)]
    [InlineData(0f, 256f, 0f, false)]
    [InlineData(1000001f, 64f, 0f, false)]
    [InlineData(float.NaN, 64f, 0f, false)]
    [InlineData(0f, float.PositiveInfinity, 0f, false)]
    public void CoordinatesAreBoundedAndFinite(float x, float y, float z, bool valid)
    {
        Assert.Equal(valid, TeleportCommandHandlers.IsValidPosition(new Vector3(x, y, z)));
    }
}
