using Game.Network;
using Game.Network.Packages;

using LiteNetLib;

namespace Survivalcraft.Test.Network;

public sealed class InventoryTransportPolicyTest
{
    [Fact]
    public void FurnaceStateSharesEntityLifecycleChannel()
    {
        Assert.Equal(PackageTransportPolicy.Get(new EntityPackage { Type = EntityPackage.EventType.LoadOne }),
            PackageTransportPolicy.Get(new ComponentFurnacePackage()));
        Assert.Equal(PackageTransportPolicy.Bulk, PackageTransportPolicy.Get(new ComponentFurnacePackage()));
    }

    [Fact]
    public void ContainerOpenReplyCannotOvertakeEntityBaseline()
    {
        Assert.Equal(PackageTransportPolicy.Bulk,
            PackageTransportPolicy.Get(new BlockEditPackage { Type = BlockEditPackage.EventType.OpenInventoryByID }));
        Assert.Equal(PackageTransportPolicy.Control,
            PackageTransportPolicy.Get(new BlockEditPackage { Type = BlockEditPackage.EventType.OpenInventoryByPoint }));
    }

    [Theory]
    [InlineData(EntityPackage.EventType.LoadOne)]
    [InlineData(EntityPackage.EventType.LoadList)]
    [InlineData(EntityPackage.EventType.Remove)]
    public void EntityLifecycleSharesOrderedChannelWithInventoryState(EntityPackage.EventType type)
    {
        var entityTransport = PackageTransportPolicy.Get(new EntityPackage { Type = type });
        var inventoryTransport = PackageTransportPolicy.Get(new ComponentInventoryPackage
        {
            PackageEventType = ComponentInventoryPackage.EventType.InventorySync
        });

        Assert.Equal(PackageTransportPolicy.Bulk, entityTransport);
        Assert.Equal(entityTransport, inventoryTransport);
        Assert.Equal(DeliveryMethod.ReliableOrdered, entityTransport.DeliveryMethod);
    }

    [Theory]
    [InlineData(ComponentInventoryPackage.EventType.ActiveSlotChange)]
    [InlineData(ComponentInventoryPackage.EventType.SetSlotsItem)]
    public void InventoryMutationsCannotOvertakeCreation(ComponentInventoryPackage.EventType type)
    {
        Assert.Equal(PackageTransportPolicy.Bulk,
            PackageTransportPolicy.Get(new ComponentInventoryPackage { PackageEventType = type }));
    }

    [Theory]
    [InlineData(ComponentInventoryPackage.EventType.HandleMoveItem)]
    [InlineData(ComponentInventoryPackage.EventType.HandleDragDrop)]
    public void InventoryOperationsRemainOnControlChannel(ComponentInventoryPackage.EventType type)
    {
        Assert.Equal(PackageTransportPolicy.Control,
            PackageTransportPolicy.Get(new ComponentInventoryPackage { PackageEventType = type }));
        Assert.Equal(PackageTransportPolicy.Control,
            PackageTransportPolicy.Get(new EntityPackage { Type = EntityPackage.EventType.RequestSync }));
    }
}
