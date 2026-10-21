namespace Game.Protocol.Entities;

public sealed record InventorySlot
{
    public int Count { get; init; }

    public int InventoryId { get; init; }

    public int SlotIndex { get; init; }

    public int Value { get; init; }
}
