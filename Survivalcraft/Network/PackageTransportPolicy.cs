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

    public static readonly PackageTransport TerrainBulk =
        new(NetworkChannel.TerrainBulk, DeliveryMethod.ReliableUnordered, 0.10);

    public static readonly PackageTransport TerrainFragment =
        new(NetworkChannel.TerrainFragment, DeliveryMethod.Unreliable, 0.02);

    /// <summary>按包类型解析传输方式。新包类型在此注册。</summary>
    public static PackageTransport Get(IPackage package)
    {
        return package switch
        {
            ComponentPlayerPackage { Type: ComponentPlayerPackage.PlayerAction.BodyUpdate } => Snapshot,
            SubsystemBodyPackage { PackageEventType: SubsystemBodyPackage.EventType.BodyUpdate } => StateStream,
            PickablePackage { Type: PickablePackage.PickType.Update } => Snapshot,
            OnlinePlayerStatePackage => Snapshot,
            ComponentBehaviorPackage { PackageEventType: ComponentBehaviorPackage.EventType.CreatureSound } => Effect,
            ComponentHealthPackage { Type: ComponentHealthPackage.EventType.HitResult } => Effect,
            ExplosionsPackage { Type: ExplosionsPackage.EventType.Sound } => Effect,
            ProjectilePackage => ReliableEvent,
            MovingBlockPackage => ReliableEvent,
            ComponentPlayerPackage => ReliableEvent,
            ComponentHealthPackage => ReliableEvent,
            ComponentMountPackage => ReliableEvent,
            ComponentOnFirePackage => ReliableEvent,
            ComponentSleepPackage => ReliableEvent,
            PickablePackage => ReliableEvent,
            ExplosionsPackage => ReliableEvent,
            EntityPackage { Type: EntityPackage.EventType.LoadList } => Bulk,
            BootstrapPackage => Bulk,
            InitialWorldSnapshotPackage => Bulk,
            PlayerJoinedPackage => Bulk,
            SubsystemTerrainPackage { Type: SubsystemTerrainPackage.DataType.SyncTerrainChunkFragment } =>
                TerrainFragment,
            FurniturePackage
            {
                PackageEventType: FurniturePackage.EventType.Add or
                FurniturePackage.EventType.TryAddDesignChain
            } => Bulk,
            _ => Control
        };
    }
}
