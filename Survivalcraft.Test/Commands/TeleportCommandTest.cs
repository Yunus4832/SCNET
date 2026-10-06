using Engine.Core;

using Game.Commands;
using Game.Components;

namespace Survivalcraft.Test.Commands;

public class TeleportCommandTest
{
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
