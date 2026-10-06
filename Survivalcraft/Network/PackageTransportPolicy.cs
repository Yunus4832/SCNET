using Game.Network.Packages;

using LiteNetLib;

namespace Game.Network;

/// <summary>
///     传输方式注册表：数据包保持简单，不声明传输方式；
///     由这里按包类型集中解析（<see cref="Get" />），便于统一调整与动态扩展。
/// </summary>
public static class PackageTransportPolicy
{
    public static readonly PackageTransport Control =
        new(NetworkChannel.Control, DeliveryMethod.ReliableOrdered, 0.02);

    public static readonly PackageTransport ReliableEvent =
        new(NetworkChannel.ReliableEvent, DeliveryMethod.ReliableOrdered, 0.03);

    public static readonly PackageTransport Snapshot =
        new(NetworkChannel.Snapshot, DeliveryMethod.Sequenced, 0.05, true);

    /// <summary>
    ///     状态流：一个逻辑快照拆成多个小包时，Unreliable 没有按数据报去重，
    ///     同 tick 的兄弟包互不淘汰；丢包只影响包内几只生物，下一轮自动恢复。
    ///     最新优先由应用层 StateTick 按实体比较实现。
    /// </summary>
    public static readonly PackageTransport StateStream =
        new(NetworkChannel.StateStream, DeliveryMethod.Unreliable, 0.05);

    public static readonly PackageTransport Effect =
        new(NetworkChannel.Effect, DeliveryMethod.Unreliable, 0.05);

    public static readonly PackageTransport Bulk =
        new(NetworkChannel.Bulk, DeliveryMethod.ReliableOrdered, 0.10);

    // Whole-list metadata must remain atomic; reliable transport supports fragmentation.
    public static readonly PackageTransport PlayerListState =
        new(NetworkChannel.Bulk, DeliveryMethod.ReliableOrdered, 0.10, true);

    public static readonly PackageTransport TerrainFragment =
        new(NetworkChannel.TerrainFragment, DeliveryMethod.Unreliable, 0.02);

    /// <summary>按包类型解析传输方式。新包类型在此注册。</summary>
    public static PackageTransport Get(IPackage package)
    {
        return package switch
        {
            ComponentPlayerPackage { Type: ComponentPlayerPackage.PlayerAction.BodyUpdate } => Snapshot,
            SubsystemBodyPackage { PackageEventType: SubsystemBodyPackage.EventType.BodyUpdate } => StateStream,
            PickablePackage { Type: PickablePackage.PickType.Update } => StateStream,
            OnlinePlayerStatePackage => PlayerListState,
            ComponentBehaviorPackage { PackageEventType: ComponentBehaviorPackage.EventType.CreatureSound } => Effect,
            ComponentHealthPackage { Type: ComponentHealthPackage.EventType.HitResult } => Effect,
            ExplosionsPackage { Type: ExplosionsPackage.EventType.Sound } => Effect,
            ProjectilePackage { Type: ProjectilePackage.EventType.Update } => StateStream,
            ProjectilePackage { Type: ProjectilePackage.EventType.Effect } => Effect,
            ProjectilePackage => ReliableEvent,
            MovingBlockPackage { Type: MovingBlockPackage.EventType.Update } => StateStream,
            MovingBlockPackage { Type: MovingBlockPackage.EventType.PistonSound } => Effect,
            MovingBlockPackage => ReliableEvent,
            ComponentPlayerPackage => ReliableEvent,
            ComponentHealthPackage => ReliableEvent,
            ComponentMountPackage
            {
                Type: ComponentMountPackage.EventType.Mount or ComponentMountPackage.EventType.Dismount
            } => Bulk,
            ComponentMountPackage => Control,
            ComponentOnFirePackage { Type: not ComponentOnFirePackage.EventType.ComponentOnFire } => Bulk,
            ComponentOnFirePackage => ReliableEvent,
            ComponentSleepPackage => ReliableEvent,
            PickablePackage => ReliableEvent,
            ExplosionsPackage => ReliableEvent,
            // 对象创建、删除及其库存状态必须共享顺序，不能跨通道抢先应用。
            EntityPackage { Type: not EntityPackage.EventType.RequestSync } => Bulk,
            ComponentInventoryPackage
            {
                PackageEventType: ComponentInventoryPackage.EventType.InventorySync or
                ComponentInventoryPackage.EventType.SetSlotsItem or
                ComponentInventoryPackage.EventType.ActiveSlotChange
            } => Bulk,
            BootstrapPackage => Bulk,
            InitialWorldSnapshotPackage => Bulk,
            EditableBlockPackage => Bulk,
            SignBlockPackage => Bulk,
            ChunkStateResetPackage => Bulk,
            BlockEditPackage { Type: BlockEditPackage.EventType.OpenInventoryByID } => Bulk,
            ComponentFurnacePackage => Bulk,
            SubsystemTerrainPackage { Type: SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment } =>
                TerrainFragment,
            FurniturePackage { PackageEventType: FurniturePackage.EventType.RequestAdd } => Control,
            FurniturePackage => Bulk,
            _ => Control
        };
    }
}
