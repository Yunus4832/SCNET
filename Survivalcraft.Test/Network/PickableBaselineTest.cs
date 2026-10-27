using Engine.Core.Mathematics;

using Game.Network.Handling;
using Game.Subsystems;
using Game.World;

namespace Survivalcraft.Test.Network;

public sealed class PickableBaselineTest
{
    [Fact]
    public void MotionSnapshotClearsStaleClientEscapeAndVelocity()
    {
        var pickable = new Pickable
        {
            LastStateTick = 10,
            Velocity = new Vector3(6, 0, 0),
            FlyToPosition = new Vector3(10, 10, 10),
            ToRemove = true
        };
        var snapshot = new PickablePosition(0, new Vector3(1, 2, 3));

        SubsystemPickables.ApplyMotionSnapshot(pickable, snapshot, 11);

        Assert.Equal(snapshot.Position, pickable.NetworkPosition);
        Assert.Equal(Vector3.Zero, pickable.Velocity);
        Assert.Null(pickable.FlyToPosition);
        Assert.False(pickable.ToRemove);

        SubsystemPickables.ApplyMotionSnapshot(
            pickable,
            new PickablePosition(0, Vector3.Zero),
            10
        );
        Assert.Equal(snapshot.Position, pickable.NetworkPosition);
        Assert.Null(pickable.FlyToPosition);
        SubsystemPickables.UpdateClientPosition(pickable, 0.1f);
        Assert.InRange(pickable.Position.X, 0.8f, 1f);
        SubsystemPickables.UpdateClientPosition(pickable, 1f);
        Assert.True(Vector3.Distance(pickable.Position, snapshot.Position) < 0.001f);
    }

    [Fact]
    public void RecoveryBaselineCorrectsExistingPickablePositionAndState()
    {
        var pickable = new Pickable { Id = 7, Position = Vector3.Zero, Count = 1 };
        var authoritative = new Pickable
        {
            Id = 7,
            Value = 42,
            Count = 3,
            Position = new Vector3(10, 20, 30),
            Velocity = new Vector3(1, 2, 3),
            StuckMatrix = Matrix.Identity
        };

        PickablePackageHandler.ApplyBaseline(
            pickable,
            PickablePackageBuilder.Capture(authoritative) with { StateTick = 42 }
        );

        Assert.Equal(authoritative.Id, pickable.Id);
        Assert.Equal(authoritative.Value, pickable.Value);
        Assert.Equal(authoritative.Count, pickable.Count);
        Assert.Equal(authoritative.Position, pickable.Position);
        Assert.Equal(authoritative.Velocity, pickable.Velocity);
        Assert.Equal(authoritative.StuckMatrix, pickable.StuckMatrix);
        Assert.Equal(42u, pickable.LastStateTick);
    }
}
