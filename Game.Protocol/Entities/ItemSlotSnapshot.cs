using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ItemSlotSnapshot : InventorySlotSnapshot
{
    [Key(1)] public int Count { get; init; }

    [Key(2)] public int Value { get; init; }
}
