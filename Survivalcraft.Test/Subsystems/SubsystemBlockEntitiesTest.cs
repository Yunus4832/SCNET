using Engine.Core;

using Game.Components;
using Game.Subsystems;

namespace Survivalcraft.Test.Subsystems;

public sealed class SubsystemBlockEntitiesTest
{
    [Fact]
    public void RemovingRejectedDuplicateDoesNotUnregisterExistingBlockEntity()
    {
        var subsystem = new SubsystemBlockEntities();
        var point = new Point3(1, 2, 3);
        var existing = new ComponentBlockEntity { Coordinates = point };
        var duplicate = new ComponentBlockEntity { Coordinates = point };
        subsystem.BlockEntities.Add(point, existing);

        Assert.False(subsystem.RemoveBlockEntity(duplicate));
        Assert.Same(existing, subsystem.GetBlockEntity(point.X, point.Y, point.Z));
        Assert.True(subsystem.RemoveBlockEntity(existing));
        Assert.Null(subsystem.GetBlockEntity(point.X, point.Y, point.Z));
    }
}
