using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(InventoryActiveSlotPackage))]
[Union(1, typeof(InventoryDragDropRequestPackage))]
[Union(2, typeof(InventoryMoveRequestPackage))]
[Union(3, typeof(InventorySetSlotPackage))]
[Union(4, typeof(InventorySnapshotPackage))]
[Union(5, typeof(InventorySyncRequestPackage))]
public abstract record ComponentInventoryPackage : IProtocolMessage
{
    public enum EventType
    {
        ActiveSlotChange = 0, InventorySync = 1, QueryErrorInventoryInfo = 2,

        // Tags 3 and 5 retired: unimplemented furnace/full-inventory modes.
        SetSlotsItem = 4, HandleMoveItem = 6, HandleDragDrop = 7
    }

    [IgnoreMember] public abstract EventType PackageEventType { get; }
}
