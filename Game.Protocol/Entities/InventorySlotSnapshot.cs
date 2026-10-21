namespace Game.Protocol.Entities;

public sealed record InventorySlotSnapshot
{
    public int SlotIndex { get; init; }

    public byte Type { get; init; }

    public int Count { get; init; }

    public int Value { get; init; }

    public IReadOnlyList<int> ClothingList { get; init; } = [];
}
