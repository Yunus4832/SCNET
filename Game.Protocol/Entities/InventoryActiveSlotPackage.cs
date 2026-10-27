using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record InventoryActiveSlotPackage : ComponentInventoryPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.ActiveSlotChange;

    [Key(0)] public int InventoryID { get; init; }

    [Key(1)] public int ActiveSlot { get; init; }
}
