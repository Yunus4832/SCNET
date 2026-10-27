using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record InventorySetSlotPackage : ComponentInventoryPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.SetSlotsItem;

    [Key(0)] public int InventoryId { get; init; }

    [Key(1)] public int SlotIndex { get; init; }

    [Key(2)] public int Value { get; init; }

    [Key(3)] public int Count { get; init; }
}
