using Game.Network.Packages;

using LiteNetLib;

namespace Game.Network;

public static class PackageTransportPolicy
{
    public static readonly PackageTransport Control =
        new(NetworkChannel.Control, DeliveryMethod.ReliableOrdered, 0.02);

    public static readonly PackageTransport ReliableEvent =
        new(NetworkChannel.ReliableEvent, DeliveryMethod.ReliableOrdered, 0.03);

    public static readonly PackageTransport Snapshot =
        new(NetworkChannel.Snapshot, DeliveryMethod.Sequenced, 0.05, true);

    public static readonly PackageTransport Effect =
        new(NetworkChannel.Effect, DeliveryMethod.Unreliable, 0.05);

    public static readonly PackageTransport Bulk =
        new(NetworkChannel.Bulk, DeliveryMethod.ReliableOrdered, 0.10);

    public static PackageTransport Get(IPackage package)
    {
        return package switch
        {
            ComponentPlayerPackage { Type: ComponentPlayerPackage.PlayerAction.BodyUpdate } => Snapshot,
            SubsystemBodyPackage { PackageEventType: SubsystemBodyPackage.EventType.BodyUpdate } => Snapshot,
            PickablePackage { Type: PickablePackage.PickType.Update } => Snapshot,
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
            ProjectPackage => Bulk,
            SubsystemTerrainPackage { Type: SubsystemTerrainPackage.DataType.SyncTerrainChunkList } => Bulk,
            FurniturePackage
            {
                PackageEventType: FurniturePackage.EventType.Add or
                    FurniturePackage.EventType.TryAddDesignChain
            } => Bulk,
            _ => Control
        };
    }
}
