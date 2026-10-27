using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record InventorySlotReference
{
    [Key(0)] public int InventoryId { get; init; }

    [Key(1)] public int SlotIndex { get; init; }
}
