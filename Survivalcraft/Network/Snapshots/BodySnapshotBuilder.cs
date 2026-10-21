namespace Game.Network.Snapshots;

public static class BodySnapshotBuilder
{
    public static SubsystemBodyPackage Capture(IEnumerable<ComponentBody> bodies, uint tick) => new()
    {
        PackageEventType = SubsystemBodyPackage.EventType.BodyUpdate,
        StateTick = tick,
        BodyList = bodies.Select(Capture).ToArray()
    };

    public static SubsystemBodyPackage.BodyItem Capture(ComponentBody body)
    {
        // 每轮发送完整当前状态（位置/旋转/速度/视线角），快照自包含：
        // 丢包后下一轮即恢复，不会因“停止移动前最后一包丢失”而长期停在旧位置。
        var bodyItem = new SubsystemBodyPackage.BodyItem
        {
            CreatureId = body.Entity.EntityId
        };
        bodyItem.ChangeFlag |= SubsystemBodyPackage.ChangeFlag.PositionChange;
        bodyItem.Position = body.Position;
        bodyItem.ChangeFlag |= SubsystemBodyPackage.ChangeFlag.RotationChange;
        bodyItem.Rotation = body.Rotation;
        bodyItem.ChangeFlag |= SubsystemBodyPackage.ChangeFlag.VelocityChange;
        bodyItem.Velocity = body.Velocity;

        if (body.Locomotion != null)
        {
            bodyItem.ChangeFlag |= SubsystemBodyPackage.ChangeFlag.LookAnglesChange;
            bodyItem.LookAngles = body.Locomotion.LookAngles;

            if (body.Locomotion.FlyOrderChange)
            {
                bodyItem.ChangeFlag |= SubsystemBodyPackage.ChangeFlag.FlyOrderChange;
                bodyItem.FlyOrder = body.Locomotion.LastFlyOrder;
            }
        }

        return bodyItem;
    }
}
