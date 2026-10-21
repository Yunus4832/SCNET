using Game.Protocol.Blocks;
using Game.Protocol.Terrain;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteEditableBlockPackage(EditableBlockPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.ItemType);
        writer.Write(message.SyncItem);
        if (message.SyncItem)
        {
            writer.Write(message.SlotIndex);
        }
        else
        {
            writer.Write(message.EditAsItem);
            if (message.EditAsItem)
            {
                writer.Write(message.InventoryId);
                writer.Write(message.SlotIndex);
            }
            else
            {
                writer.Write(message.CellFace);
            }
        }

        switch (message.ItemType)
        {
            case BuiltInBlockEditKind.TruthTable:
            case BuiltInBlockEditKind.MemoryBank:
                writer.Write(message.Id.HasValue);
                if (message.Id.HasValue)
                {
                    writer.Write(message.Id.Value);
                }

                if (message.Data.Length > ushort.MaxValue)
                {
                    throw new ArgumentException("Editable block data is too long.");
                }

                writer.Write((ushort)message.Data.Length);
                writer.Write(message.Data);
                break;
            default:
                writer.Write(message.Delay);
                break;
        }
    }

    private static EditableBlockPackage ReadEditableBlockPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<BuiltInBlockEditKind>();
        if (!Enum.IsDefined(type)) { throw new InvalidDataException("Unknown editable item type."); }

        var sync = reader.ReadBoolean();
        var slot = 0;
        var inventory = 0;
        var asItem = false;
        var cell = default(CellFace);
        if (sync) { slot = reader.ReadInt32(); }
        else
        {
            asItem = reader.ReadBoolean();
            if (asItem)
            {
                inventory = reader.ReadInt32();
                slot = reader.ReadInt32();
            }
            else { cell = reader.ReadCellFace(); }
        }

        int? id = null;
        var data = Array.Empty<byte>();
        var delay = 0;
        if (type is BuiltInBlockEditKind.TruthTable or BuiltInBlockEditKind.MemoryBank)
        {
            if (reader.ReadBoolean()) { id = reader.ReadInt32(); }

            data = reader.ReadBytes(reader.ReadUInt16());
        }
        else { delay = reader.ReadInt32(); }

        return new EditableBlockPackage
        {
            ItemType = type, SyncItem = sync, SlotIndex = slot, InventoryId = inventory, EditAsItem = asItem,
            CellFace = cell, Id = id, Data = data, Delay = delay
        };
    }
}
