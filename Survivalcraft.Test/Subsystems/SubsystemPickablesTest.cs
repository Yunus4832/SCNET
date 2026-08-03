using Engine.Core;

using Game;
using Game.Subsystems;

namespace Survivalcraft.Test.Subsystems;

public sealed class SubsystemPickablesTest
{
    [Fact]
    public void AllocatorProducesUniqueIdsAndReusesReleasedId()
    {
        var subsystem = new SubsystemPickables();

        Assert.Equal((ushort)1, subsystem.FindAvailableId());
        Assert.Equal((ushort)2, subsystem.FindAvailableId());

        var pickable = subsystem.CreatePickable(
            42,
            1,
            1,
            Vector3.Zero,
            Vector3.Zero,
            null);

        Assert.NotNull(pickable);
        Assert.Null(subsystem.CreatePickable(42, 1, 1, Vector3.Zero, Vector3.Zero, null));
        Assert.True(subsystem.TryGetPickable(42, out var indexed));
        Assert.Same(pickable, indexed);

        Assert.True(subsystem.RemovePickable(pickable));
        Assert.False(subsystem.TryGetPickable(42, out _));
        Assert.NotNull(subsystem.CreatePickable(42, 1, 1, Vector3.Zero, Vector3.Zero, null));
    }

    [Fact]
    public void RemovingUnknownPickableDoesNotCorruptIndexes()
    {
        var subsystem = new SubsystemPickables();
        var registered = subsystem.CreatePickable(7, 1, 1, Vector3.Zero, Vector3.Zero, null);

        Assert.NotNull(registered);
        Assert.False(subsystem.RemovePickable(new Pickable { Id = 7 }));
        Assert.True(subsystem.TryGetPickable(7, out var indexed));
        Assert.Same(registered, indexed);
    }
}
