using Engine.Core;

using Game;
using Game.Subsystems;

namespace Survivalcraft.Test.Subsystems;

public sealed class SubsystemPickablesTest
{
    [Fact]
    public void PositionSnapshotsRejectDuplicatesAndOlderTicksAcrossWraparound()
    {
        var subsystem = new SubsystemPickables();
        var pickable = subsystem.CreatePickable(1, 1, 1, Vector3.Zero, Vector3.Zero, null)!;
        pickable.LastStateTick = uint.MaxValue - 1;
        var position = new Vector3(4f, 5f, 6f);
        subsystem.ApplyPositionSnapshot([new Pickable { Id = 1, Position = position }], 1);
        subsystem.ApplyPositionSnapshot([new Pickable { Id = 1, Position = Vector3.Zero }], 1);
        subsystem.ApplyPositionSnapshot([new Pickable { Id = 1, Position = Vector3.Zero }], uint.MaxValue);
        Assert.Equal(position, pickable.NetworkPosition);
        Assert.Equal(1u, pickable.LastStateTick);
    }

    [Fact]
    public void SameTickIndependentChunksUpdateDifferentPickables()
    {
        var subsystem = new SubsystemPickables();
        var first = subsystem.CreatePickable(1, 1, 1, Vector3.Zero, Vector3.Zero, null)!;
        var second = subsystem.CreatePickable(2, 1, 1, Vector3.Zero, Vector3.Zero, null)!;
        subsystem.ApplyPositionSnapshot([new Pickable { Id = 1, Position = Vector3.One }], 10);
        subsystem.ApplyPositionSnapshot([new Pickable { Id = 2, Position = Vector3.One }], 10);
        Assert.Equal(Vector3.One, first.NetworkPosition);
        Assert.Equal(Vector3.One, second.NetworkPosition);
    }

    [Fact]
    public void PositionSnapshotDoesNotDeleteAbsentPickables()
    {
        var subsystem = new SubsystemPickables();
        var updated = subsystem.CreatePickable(1, 1, 1, Vector3.Zero, Vector3.Zero, null)!;
        var retained = subsystem.CreatePickable(2, 1, 1, Vector3.Zero, Vector3.Zero, null)!;
        var position = new Vector3(10f, 20f, 30f);

        subsystem.ApplyPositionSnapshot([new Pickable { Id = 1, Position = position }], 1);
        subsystem.ApplyPositionSnapshot([], 2);

        Assert.Equal(position, updated.NetworkPosition);
        Assert.True(subsystem.TryGetPickable(2, out var indexed));
        Assert.Same(retained, indexed);
        Assert.Empty(subsystem.PickablesToRemove);
    }

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
