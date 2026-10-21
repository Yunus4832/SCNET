namespace Game.Network.Snapshots;

public static class EditableBlockPackageBuilder
{
    public static EditableBlockPackage Capture(
        CellFace cell,
        bool asItem,
        int inventory,
        int slot,
        TruthTableData data
    ) => new()
    {
        ItemType = BuiltInBlockEditKind.TruthTable, CellFace = cell, EditAsItem = asItem, InventoryId = inventory,
        SlotIndex = slot, Data = data.Data.ToArray()
    };

    public static EditableBlockPackage Capture(int id, TruthTableData data) => new()
        { ItemType = BuiltInBlockEditKind.TruthTable, SlotIndex = id, SyncItem = true, Data = data.Data.ToArray() };

    public static EditableBlockPackage Capture(
        CellFace cell,
        bool asItem,
        int inventory,
        int slot,
        MemoryBankData data
    ) => new()
    {
        ItemType = BuiltInBlockEditKind.MemoryBank, CellFace = cell, EditAsItem = asItem, InventoryId = inventory,
        SlotIndex = slot, Data = data.Data.ToArray()
    };

    public static EditableBlockPackage Capture(int id, MemoryBankData data) => new()
        { ItemType = BuiltInBlockEditKind.MemoryBank, SlotIndex = id, SyncItem = true, Data = data.Data.ToArray() };
}
