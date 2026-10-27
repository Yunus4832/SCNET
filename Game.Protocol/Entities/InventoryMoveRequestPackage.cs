using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record InventoryMoveRequestPackage : ComponentInventoryPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.HandleMoveItem;

    [Key(0)] public required InventorySlotReference Source { get; init; }

    [Key(1)] public required InventorySlotReference Target { get; init; }

    [Key(2)] public int Count { get; init; }
}
