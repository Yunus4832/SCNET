using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
[Union(0, typeof(ItemSlotSnapshot))]
[Union(1, typeof(ClothingSlotSnapshot))]
public abstract record InventorySlotSnapshot
{
    [Key(0)] public int SlotIndex { get; init; }
}
