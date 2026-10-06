using Engine.Core;

using Game.Automation;

namespace Survivalcraft.Test.Automation;

public sealed class AutomationViewSteeringTest
{
    [Theory]
    [InlineData(float.NaN, 1, 10)]
    [InlineData(0, 0, 10)]
    [InlineData(0, 6, 10)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 1, 31)]
    public void InvalidViewRequestFailsBeforeAccessingCharacter(float x, float tolerance, double timeout)
    {
        var controller = new AutomationViewController();
        Assert.Throws<ArgumentException>(() => controller.Start(null!, new Vector3(x, 0, 1), tolerance, timeout));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 1)]
    [InlineData(90, 0, 1, 0, 0)]
    [InlineData(180, 0, 0, 0, -1)]
    [InlineData(450, 0, 1, 0, 0)]
    public void AnglesHaveStableWorldAxisConvention(float yaw, float pitch, float x, float y, float z)
    {
        var direction = AutomationViewSteering.Direction(yaw, pitch);
        Assert.InRange(Vector3.Distance(direction, new Vector3(x, y, z)), 0f, 0.00001f);
    }

    [Theory]
    [InlineData(float.NaN, 0)]
    [InlineData(0, float.PositiveInfinity)]
    [InlineData(0, 83)]
    [InlineData(0, -83)]
    public void InvalidAnglesAreRejected(float yaw, float pitch)
    {
        Assert.Throws<ArgumentException>(() => AutomationViewSteering.Direction(yaw, pitch));
    }

    [Fact]
    public void AlignedDirectionDoesNotTurn()
    {
        Assert.Equal(Vector2.Zero, AutomationViewSteering.Calculate(Vector3.UnitZ, Vector3.UnitZ));
    }

    [Fact]
    public void VerticalTargetDoesNotProduceInvalidYaw()
    {
        var look = AutomationViewSteering.Calculate(Vector3.UnitZ, Vector3.UnitY);
        Assert.Equal(0f, look.X);
        Assert.Equal(1f, look.Y);
    }

    [Fact]
    public void PitchTurnsTowardTargetAndIsBounded()
    {
        Assert.Equal(-1f, AutomationViewSteering.Calculate(Vector3.UnitZ, -Vector3.UnitY).Y);
        var target = Vector3.Normalize(new Vector3(0, 0.1f, 1));
        var look = AutomationViewSteering.Calculate(Vector3.UnitZ, target);
        Assert.InRange(look.Y, 0.1f, 0.3f);
        Assert.Equal(0f, look.X);
    }

    [Fact]
    public void OppositeDirectionHasBoundedTurn()
    {
        var look = AutomationViewSteering.Calculate(Vector3.UnitZ, -Vector3.UnitZ);
        Assert.Equal(1f, Math.Abs(look.X));
        Assert.Equal(0f, look.Y);
    }
}
