using Game.Protocol.Terrain;

namespace Game.Protocol.Blocks;

public sealed record EditableBlockPackage : IProtocolMessage
{
    public CellFace CellFace { get; init; }

    public byte[] Data { get; init; } = [];

    public int Delay { get; init; }

    public bool EditAsItem { get; init; }

    public int? Id { get; init; }

    public int InventoryId { get; init; }

    public BuiltInBlockEditKind ItemType { get; init; }

    public int SlotIndex { get; init; }

    public bool SyncItem { get; init; }

    public byte ID => (byte)PackageType.EditableBlock;

    public EditableBlockPackage()
    {
    }

    public EditableBlockPackage(
        BuiltInBlockEditKind itemType,
        CellFace cell,
        bool editAsItem,
        int inventoryId,
        int slotIndex,
        int delay
    )
    {
        ItemType = itemType;
        CellFace = cell;
        EditAsItem = editAsItem;
        InventoryId = inventoryId;
        SlotIndex = slotIndex;
        Delay = delay;
    }
}
