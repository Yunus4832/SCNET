namespace Game.Network.Snapshots;

public static class PickablePackageBuilder
{
    public static PickablePackage Capture(Pickable pickable, PickablePackage.PickType type) => new()
    {
        StateTick = unchecked((uint)Time.FrameIndex),
        Type = type,
        Id = pickable.Id,
        Value = pickable.Value,
        Count = (byte)pickable.Count,
        Position = pickable.Position,
        Velocity = pickable.Velocity,
        StuckMatrix = pickable.StuckMatrix,
        PlaySound = pickable.PlaySound
    };

    public static PickablePackage Capture(
        IEnumerable<Pickable> items,
        PickablePackage.PickType type = PickablePackage.PickType.Update
    )
    {
        var tick = unchecked((uint)Time.FrameIndex);
        return type switch
        {
            PickablePackage.PickType.Update => new PickablePackage
            {
                Type = type, StateTick = tick,
                Positions = items.Select(p => new PickablePosition(p.Id, p.Position)).ToArray()
            },
            PickablePackage.PickType.CreateList => new PickablePackage
            {
                Type = type, StateTick = tick,
                Pickables = items.Select(p =>
                    new PickableSnapshot(p.Id, p.Count, p.Value, p.Position, p.Velocity, p.StuckMatrix)
                ).ToArray()
            },
            PickablePackage.PickType.DeleteList => new PickablePackage
                { Type = type, StateTick = tick, RemovedIds = items.Select(p => p.Id).ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
