using Engine.Core;

using Game.Automation;

namespace Survivalcraft.Test.Automation;

public sealed class AutomationNavigationSteeringTest
{
    [Fact]
    public void FinalGridPointDoesNotEndMovementBeforeRequestedArrivalRange()
    {
        var destination = new Vector3(-128, 65, -141.5f);
        var waypoint = new Vector3(-128.5f, 65, -141.5f);
        var position = new Vector3(-128.76479f, 65, -141.49632f);
        Assert.True(Vector3.Distance(position, waypoint) < 0.6f);
        Assert.True(Vector3.Distance(position, destination) > 0.75f);
        Assert.Equal(destination, AutomationNavigationSteering.SelectTarget(waypoint, destination, 0.75f, true));
        Assert.Equal(waypoint, AutomationNavigationSteering.SelectTarget(waypoint, destination, 0.75f, false));
    }

    [Fact]
    public void PartialPathDoesNotSkipUnsearchedTerrainToDestination()
    {
        Assert.Equal(Vector3.UnitZ, AutomationNavigationSteering.SelectTarget(Vector3.UnitZ,
            new Vector3(0, 0, 10), 0.75f, true));
    }

    [Fact]
    public void FacingWaypointWalksForwardWithoutTurning()
    {
        var input = AutomationNavigationSteering.Calculate(Vector3.Zero, Vector3.UnitZ, new Vector3(0, 0, 5));
        Assert.Equal(0.8f, input.Move.Z);
        Assert.Equal(Vector2.Zero, input.Look);
        Assert.Equal(input.Move, input.SneakMove);
    }

    [Fact]
    public void BehindWaypointTurnsBeforeWalking()
    {
        var input = AutomationNavigationSteering.Calculate(Vector3.Zero, Vector3.UnitZ, -Vector3.UnitZ);
        Assert.Equal(Vector3.Zero, input.Move);
        Assert.Equal(1f, Math.Abs(input.Look.X));
    }

    [Fact]
    public void VerticalOnlyWaypointDoesNotProduceInvalidDirection()
    {
        var input = AutomationNavigationSteering.Calculate(Vector3.Zero, Vector3.UnitZ, Vector3.UnitY);
        Assert.Equal(Vector3.Zero, input.Move);
        Assert.Equal(Vector2.Zero, input.Look);
    }

    [Theory]
    [InlineData(float.NaN, 65, 0, 1, 30)]
    [InlineData(0, 65, 0, 0, 30)]
    [InlineData(0, 65, 0, 1, 121)]
    public void InvalidDestinationContractFailsBeforeWorldAccess(float x, float y, float z, float range, double timeout)
    {
        var navigation = new AutomationNavigationController();
        Assert.Throws<ArgumentException>(() => navigation.Start(new Vector3(x, y, z), range, timeout));
    }
}
