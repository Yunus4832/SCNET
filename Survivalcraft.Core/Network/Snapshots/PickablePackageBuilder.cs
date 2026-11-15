namespace Game.Network.Snapshots;

public static class PickablePackageBuilder
{
    public static PickableAddedPackage Capture(Pickable pickable, uint stateTick) => new()
    {
        StateTick = stateTick, Id = pickable.Id, Value = pickable.Value,
        Count = pickable.Count, Position = pickable.Position, Velocity = pickable.Velocity,
        StuckMatrix = pickable.StuckMatrix
    };

    public static PickableRemovedPackage CaptureRemoval(Pickable pickable, uint stateTick) => new()
    {
        StateTick = stateTick, Id = pickable.Id, PlaySound = pickable.PlaySound
    };

    public static PickablePositionsPackage Capture(IEnumerable<Pickable> items, uint stateTick) => new()
    {
        StateTick = stateTick,
        Positions = items.Select(item => new PickablePosition(item.Id, item.Position)).ToArray()
    };

    public static PickablesAddedPackage CaptureAdded(IEnumerable<Pickable> items, uint stateTick) => new()
    {
        StateTick = stateTick,
        Pickables = items.Select(item => new PickableSnapshot(
                item.Id,
                item.Count,
                item.Value,
                item.Position,
                item.Velocity,
                item.StuckMatrix
            )
        ).ToArray()
    };

    public static PickablesRemovedPackage CaptureRemoved(IEnumerable<Pickable> items, uint stateTick) => new()
    {
        StateTick = stateTick, RemovedIds = items.Select(item => item.Id).ToArray()
    };
}
