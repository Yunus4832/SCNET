using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
///     基础包模板复制
/// </summary>
public class EditableBlockPackage : IPackage
{
    public CellFace CellFace;

    public byte[] Data = [];

    public int Delay;

    public bool EditAsItem;

    public int? Id;

    public int InventoryId;

    public EditableItemType ItemType;

    public int SlotIndex;

    public bool SyncItem;

    public byte ID => (byte)PackageType.EditableBlock;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public EditableBlockPackage()
    {
    }

    public EditableBlockPackage(
        EditableItemType itemType,
        CellFace cell,
        bool editAsItem,
        int inventoryId,
        int slotIndex,
        int delay
    )
    {
        ItemType = itemType;
        InventoryId = inventoryId;
        CellFace = cell;
        EditAsItem = editAsItem;
        SlotIndex = slotIndex;
        Delay = delay;
    }

    public EditableBlockPackage(
        CellFace cell,
        bool editAsItem,
        int inventoryId,
        int slotIndex,
        TruthTableData truthTableData
    )
    {
        ItemType = EditableItemType.TruthTable;
        CellFace = cell;
        EditAsItem = editAsItem;
        InventoryId = inventoryId;
        SlotIndex = slotIndex;
        Data = truthTableData.Data.ToArray();
    }

    public EditableBlockPackage(
        CellFace cell,
        bool editAsItem,
        int inventoryId,
        int slotIndex,
        MemoryBankData memoryBankData
    )
    {
        ItemType = EditableItemType.MemoryBank;
        CellFace = cell;
        EditAsItem = editAsItem;
        InventoryId = inventoryId;
        SlotIndex = slotIndex;
        Data = memoryBankData.Data.ToArray();
    }

    public EditableBlockPackage(int id, TruthTableData truthTableData)
    {
        ItemType = EditableItemType.TruthTable;
        Data = truthTableData.Data.ToArray();
        SlotIndex = id;
        SyncItem = true;
    }

    public EditableBlockPackage(int id, MemoryBankData memoryBankData)
    {
        ItemType = EditableItemType.MemoryBank;
        Data = memoryBankData.Data.ToArray();
        SyncItem = true;
        SlotIndex = id;
    }


    public void ReadData(PackageStreamReader reader)
    {
        ItemType = reader.ReadEnum<EditableItemType>();
        SyncItem = reader.ReadBoolean();
        if (SyncItem)
        {
            SlotIndex = reader.ReadInt32();
        }
        else
        {
            EditAsItem = reader.ReadBoolean();
            if (EditAsItem)
            {
                InventoryId = reader.ReadInt32();
                SlotIndex = reader.ReadInt32();
            }
            else
            {
                CellFace = reader.ReadCellFace();
            }
        }

        switch (ItemType)
        {
            case EditableItemType.TruthTable:
            case EditableItemType.MemoryBank:
                if (reader.ReadBoolean())
                {
                    Id = reader.ReadInt32();
                }

                var count = reader.ReadUInt16();
                Data = new byte[count];
                Data = reader.ReadBytes(count);
                break;

            default:
                Delay = reader.ReadInt32();
                break;
        }
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.WriteEnum(ItemType);
        writer.Write(SyncItem);
        if (SyncItem)
        {
            writer.Write(SlotIndex);
        }
        else
        {
            writer.Write(EditAsItem);
            if (EditAsItem)
            {
                writer.Write(InventoryId);
                writer.Write(SlotIndex);
            }
            else
            {
                writer.Write(CellFace);
            }
        }

        switch (ItemType)
        {
            case EditableItemType.TruthTable:
            case EditableItemType.MemoryBank:
                writer.Write(Id.HasValue);
                if (Id.HasValue)
                {
                    writer.Write(Id.Value);
                }

                writer.Write((ushort)Data.Length);
                writer.Write(Data);
                break;
            default:
                writer.Write(Delay);
                break;
        }
    }

    public void ReplaceDataAtSlot(IInventory inventory, int slotIndex, Func<int, int> newData)
    {
        var value = inventory.GetSlotValue(slotIndex);
        var count = inventory.GetSlotCount(slotIndex);
        inventory.RemoveSlotItems(slotIndex, count);
        var newValue = Terrain.ReplaceData(value, newData(Terrain.ExtractData(value)));
        inventory.AddSlotItems(slotIndex, newValue, count);
    }
}
