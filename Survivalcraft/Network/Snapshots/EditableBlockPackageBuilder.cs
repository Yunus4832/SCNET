namespace Game.Network.Snapshots;

public static class EditableBlockPackageBuilder
{
    public static EditableBlockPackage Capture(
        CellFace cell,
        bool asItem,
        int inventory,
        int slot,
        TruthTableData data
    ) => new EditableBlockDataPackage
    {
        ItemType = BuiltInBlockEditKind.TruthTable,
        Target = asItem ? new EditableInventorySlotTarget(inventory, slot) : new EditableWorldBlockTarget(cell),
        Data = data.Data.ToArray()
    };

    public static EditableBlockPackage Capture(int id, TruthTableData data) => new EditableBlockDataPackage
    {
        ItemType = BuiltInBlockEditKind.TruthTable, Target = new EditableItemDefinitionTarget(id),
        Data = data.Data.ToArray()
    };

    public static EditableBlockPackage Capture(
        CellFace cell,
        bool asItem,
        int inventory,
        int slot,
        MemoryBankData data
    ) => new EditableBlockDataPackage
    {
        ItemType = BuiltInBlockEditKind.MemoryBank,
        Target = asItem ? new EditableInventorySlotTarget(inventory, slot) : new EditableWorldBlockTarget(cell),
        Data = data.Data.ToArray()
    };

    public static EditableBlockPackage Capture(int id, MemoryBankData data) => new EditableBlockDataPackage
    {
        ItemType = BuiltInBlockEditKind.MemoryBank, Target = new EditableItemDefinitionTarget(id),
        Data = data.Data.ToArray()
    };
}
