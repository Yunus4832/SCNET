namespace Game.Network.Snapshots;

public static class MovingBlockPackageBuilder
{
    public static MovingBlockPackage Capture(SubsystemMovingBlocks.MovingBlockSet set) => new()
    {
        Type = MovingBlockPackage.EventType.Add,
        NetworkId = set.NetworkId,
        AddData = SubsystemMovingBlocks.SaveMovingItem(set).ToMessagePack()
    };

    public static MovingBlockPackage Capture(SubsystemMovingBlocks.MovingBlockSet set, bool stop) => new()
    {
        Type = stop ? MovingBlockPackage.EventType.Stopped : MovingBlockPackage.EventType.Remove,
        NetworkId = set.NetworkId,
        Position = set.Position,
        Velocity = set.CurrentVelocity,
        Speed = set.Speed
    };
}
