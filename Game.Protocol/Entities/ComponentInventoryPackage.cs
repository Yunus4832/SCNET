using System.Collections.ObjectModel;

namespace Game.Protocol.Entities;

public sealed record ComponentInventoryPackage : IProtocolMessage
{
    public enum EventType
    {
        ActiveSlotChange = 0, InventorySync = 1, QueryErrorInventoryInfo = 2,
        // Tags 3 and 5 retired: unimplemented furnace/full-inventory modes.
        SetSlotsItem = 4, HandleMoveItem = 6, HandleDragDrop = 7
    }
    public int ActiveSlot { get; init; }
    public byte DragMode { get; init; }
    public EventType PackageEventType { get; init; }
    public int InventoryID { get; init; }
    public bool ProcessingOnly { get; init; }
    public IReadOnlyDictionary<int, IReadOnlyList<InventorySlotSnapshot>> Slots { get; init; } = ReadOnlyDictionary<int, IReadOnlyList<InventorySlotSnapshot>>.Empty;
    public InventorySlot? SourceInventorySlot { get; init; }
    public InventorySlot? TargetInventorySlot { get; init; }
    public byte ID => (byte)PackageType.ComponentInventory;
    public ComponentInventoryPackage() { }
    public ComponentInventoryPackage(int inventoryId, int activeSlot) { InventoryID = inventoryId; ActiveSlot = activeSlot; PackageEventType = EventType.ActiveSlotChange; }
    public ComponentInventoryPackage(int inventoryId, EventType type) { InventoryID = inventoryId; PackageEventType = type; }
    public ComponentInventoryPackage(InventorySlot source, InventorySlot target, EventType type) { SourceInventorySlot = source; TargetInventorySlot = target; PackageEventType = type; }
    public ComponentInventoryPackage(InventorySlot slot) { SourceInventorySlot = slot; PackageEventType = EventType.SetSlotsItem; }
}
