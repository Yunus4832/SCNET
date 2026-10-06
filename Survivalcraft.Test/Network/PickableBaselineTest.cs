using Engine.Core;

using Game;
using Game.Network.Packages;
using Game.Network.Packages.Handlers;

namespace Survivalcraft.Test.Network;

public sealed class PickableBaselineTest
{
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

        PickablePackageHandler.ApplyBaseline(pickable,
            new PickablePackage(authoritative, PickablePackage.PickType.Create) { StateTick = 42 });

        Assert.Equal(authoritative.Id, pickable.Id);
        Assert.Equal(authoritative.Value, pickable.Value);
        Assert.Equal(authoritative.Count, pickable.Count);
        Assert.Equal(authoritative.Position, pickable.Position);
        Assert.Equal(authoritative.Velocity, pickable.Velocity);
        Assert.Equal(authoritative.StuckMatrix, pickable.StuckMatrix);
        Assert.Equal(42u, pickable.LastStateTick);
    }
}
