using MessagePack;

namespace Game.Protocol.Entities;

[MessagePackObject]
public sealed record InventoryDragDropRequestPackage : ComponentInventoryPackage
{
    [IgnoreMember] public override EventType PackageEventType => EventType.HandleDragDrop;

    [Key(0)] public required InventorySlotReference Source { get; init; }

    [Key(1)] public required InventorySlotReference Target { get; init; }

    [Key(2)] public byte DragMode { get; init; }

    [Key(3)] public bool ProcessingOnly { get; init; }
}
