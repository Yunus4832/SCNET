namespace Game.Network.Snapshots;

public static class MovingBlockPackageBuilder
{
    public static MovingBlockAddedPackage Capture(SubsystemMovingBlocks.MovingBlockSet set) => new()
    {
        NetworkId = set.NetworkId,
        AddData = SubsystemMovingBlocks.SaveMovingItem(set).ToMessagePack()
    };

    public static MovingBlockPackage Capture(SubsystemMovingBlocks.MovingBlockSet set, bool stop)
    {
        return stop
            ? new MovingBlockStoppedPackage
            {
                NetworkId = set.NetworkId, Position = set.Position, Velocity = set.CurrentVelocity, Speed = set.Speed
            }
            : new MovingBlockRemovedPackage { NetworkId = set.NetworkId };
    }

    public static MovingBlockStatePackage CaptureState(SubsystemMovingBlocks.MovingBlockSet set) => new()
    {
        NetworkId = set.NetworkId, Position = set.Position, Velocity = set.CurrentVelocity, Speed = set.Speed
    };
}
