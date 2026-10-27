using Engine.Network;

using Game.Network.Transport;

namespace Survivalcraft.Test.Network;

public sealed class InventoryTransportPolicyTest
{
    [Fact]
    public void FurnaceStateSharesEntityLifecycleChannel()
    {
        Assert.Equal(
            PackageTransportPolicy.Get(new SingleEntitySnapshotPackage()),
            PackageTransportPolicy.Get(new ComponentFurnacePackage())
        );
        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(new ComponentFurnacePackage()));
    }

    [Fact]
    public void ContainerOpenReplyCannotOvertakeEntityBaseline()
    {
        Assert.Equal(
            PackageTransportPolicy.Bulk,
            PackageTransportPolicy.Get(new OpenInventoryPackage())
        );
        Assert.Equal(
            PackageTransportPolicy.Control,
            PackageTransportPolicy.Get(new OpenBlockInventoryPackage())
        );
    }

    [Theory]
    [InlineData(EntityPackage.EventType.LoadOne)]
    [InlineData(EntityPackage.EventType.LoadList)]
    [InlineData(EntityPackage.EventType.Remove)]
    public void EntityLifecycleSharesOrderedChannelWithInventoryState(EntityPackage.EventType type)
    {
        var entityTransport = PackageTransportPolicy.Get(
            (type switch
            {
                EntityPackage.EventType.Remove => (EntityPackage)new EntityRemovedPackage(),
                EntityPackage.EventType.LoadOne => new SingleEntitySnapshotPackage(),
                EntityPackage.EventType.LoadList => new EntitySnapshotPackage(),
                EntityPackage.EventType.RequestSync => new EntitySyncRequestPackage(),
                _ => throw new InvalidOperationException("Unsupported message operation.")
            })
        );
        var inventoryTransport = PackageTransportPolicy.Get(new InventorySnapshotPackage());

        Assert.Equal(PackageTransportPolicy.Bulk, entityTransport);
        Assert.Equal(entityTransport, inventoryTransport);
        Assert.Equal(DeliveryMode.ReliableOrdered, entityTransport.Delivery);
    }

    [Theory]
    [InlineData(ComponentInventoryPackage.EventType.ActiveSlotChange)]
    [InlineData(ComponentInventoryPackage.EventType.SetSlotsItem)]
    public void InventoryMutationsCannotOvertakeCreation(ComponentInventoryPackage.EventType type)
    {
        Assert.Equal(
            PackageTransportPolicy.Bulk,
            PackageTransportPolicy.Get(
                (type switch
                {
                    ComponentInventoryPackage.EventType.ActiveSlotChange => (ComponentInventoryPackage)
                        new InventoryActiveSlotPackage(),
                    ComponentInventoryPackage.EventType.InventorySync =>
                        new InventorySnapshotPackage(),
                    ComponentInventoryPackage.EventType.QueryErrorInventoryInfo =>
                        new InventorySyncRequestPackage(),
                    ComponentInventoryPackage.EventType.SetSlotsItem =>
                        new InventorySetSlotPackage(),
                    ComponentInventoryPackage.EventType.HandleMoveItem => new
                        InventoryMoveRequestPackage
                        { Source = new InventorySlotReference(), Target = new InventorySlotReference() },
                    ComponentInventoryPackage.EventType.HandleDragDrop => new
                        InventoryDragDropRequestPackage
                        { Source = new InventorySlotReference(), Target = new InventorySlotReference() },
                    _ => throw new InvalidOperationException("Unsupported message operation.")
                })
            )
        );
    }

    [Theory]
    [InlineData(ComponentInventoryPackage.EventType.HandleMoveItem)]
    [InlineData(ComponentInventoryPackage.EventType.HandleDragDrop)]
    public void InventoryOperationsRemainOnControlChannel(ComponentInventoryPackage.EventType type)
    {
        Assert.Equal(
            PackageTransportPolicy.Control,
            PackageTransportPolicy.Get(
                (type switch
                {
                    ComponentInventoryPackage.EventType.ActiveSlotChange => (ComponentInventoryPackage)
                        new InventoryActiveSlotPackage(),
                    ComponentInventoryPackage.EventType.InventorySync =>
                        new InventorySnapshotPackage(),
                    ComponentInventoryPackage.EventType.QueryErrorInventoryInfo =>
                        new InventorySyncRequestPackage(),
                    ComponentInventoryPackage.EventType.SetSlotsItem =>
                        new InventorySetSlotPackage(),
                    ComponentInventoryPackage.EventType.HandleMoveItem => new
                        InventoryMoveRequestPackage
                        { Source = new InventorySlotReference(), Target = new InventorySlotReference() },
                    ComponentInventoryPackage.EventType.HandleDragDrop => new
                        InventoryDragDropRequestPackage
                        { Source = new InventorySlotReference(), Target = new InventorySlotReference() },
                    _ => throw new InvalidOperationException("Unsupported message operation.")
                })
            )
        );
        Assert.Equal(
            PackageTransportPolicy.Control,
            PackageTransportPolicy.Get(new EntitySyncRequestPackage())
        );
    }
}
