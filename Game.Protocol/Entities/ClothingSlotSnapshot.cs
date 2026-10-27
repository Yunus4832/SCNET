using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record ClothingSlotSnapshot : InventorySlotSnapshot
{
    [Key(1), MaxLength(255)] public required IReadOnlyList<int> Clothes { get; init; }
}
