using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteComponentInventoryPackage(ComponentInventoryPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.PackageEventType);
        switch (message.PackageEventType)
        {
            case ComponentInventoryPackage.EventType.ActiveSlotChange:
                writer.Write(message.InventoryID); writer.Write(message.ActiveSlot); break;
            case ComponentInventoryPackage.EventType.QueryErrorInventoryInfo:
                writer.Write(message.InventoryID); break;
            case ComponentInventoryPackage.EventType.InventorySync:
                writer.Write(message.Slots.Count);
                foreach (var (id, slots) in message.Slots)
                {
                    writer.Write(id); writer.Write(slots.Count);
                    foreach (var slot in slots)
                    {
                        writer.Write(slot.SlotIndex); writer.Write(slot.Type);
                        if (slot.Type == 0) { writer.Write(slot.Count); writer.Write(slot.Value); }
                        else if (slot.Type == 1)
                        {
                            if (slot.ClothingList.Count > byte.MaxValue) { throw new ArgumentException("Too many clothing items."); }
                            writer.Write((byte)slot.ClothingList.Count); foreach (var value in slot.ClothingList) { writer.Write(value); }
                        }
                        else { throw new ArgumentException("Unknown inventory slot kind."); }
                    }
                }
                break;
            case ComponentInventoryPackage.EventType.HandleMoveItem:
            case ComponentInventoryPackage.EventType.HandleDragDrop:
                var source = message.SourceInventorySlot ?? throw new ArgumentException("Missing source slot.");
                var target = message.TargetInventorySlot ?? throw new ArgumentException("Missing target slot.");
                writer.Write(source.InventoryId); writer.Write(source.SlotIndex);
                if (message.PackageEventType == ComponentInventoryPackage.EventType.HandleDragDrop) { writer.Write(message.DragMode); }
                writer.Write(target.InventoryId); writer.Write(target.SlotIndex);
                if (message.PackageEventType == ComponentInventoryPackage.EventType.HandleMoveItem) { writer.Write(target.Count); }
                else { writer.Write(message.ProcessingOnly); }
                break;
            case ComponentInventoryPackage.EventType.SetSlotsItem:
                var slotValue = message.SourceInventorySlot ?? throw new ArgumentException("Missing inventory slot.");
                writer.Write(slotValue.InventoryId); writer.Write(slotValue.SlotIndex); writer.Write(slotValue.Value); writer.Write(slotValue.Count); break;
            default: throw new ArgumentException("Unknown inventory mode.");
        }
    }
    private static ComponentInventoryPackage ReadComponentInventoryPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<ComponentInventoryPackage.EventType>();
        switch (type)
        {
            case ComponentInventoryPackage.EventType.ActiveSlotChange:
                return new ComponentInventoryPackage(reader.ReadInt32(), reader.ReadInt32());
            case ComponentInventoryPackage.EventType.QueryErrorInventoryInfo:
                return new ComponentInventoryPackage(reader.ReadInt32(), type);
            case ComponentInventoryPackage.EventType.SetSlotsItem:
                return new ComponentInventoryPackage(new InventorySlot
                { InventoryId = reader.ReadInt32(), SlotIndex = reader.ReadInt32(), Value = reader.ReadInt32(), Count = reader.ReadInt32() });
            case ComponentInventoryPackage.EventType.HandleMoveItem:
            case ComponentInventoryPackage.EventType.HandleDragDrop:
                var source = new InventorySlot { InventoryId = reader.ReadInt32(), SlotIndex = reader.ReadInt32() };
                var drag = type == ComponentInventoryPackage.EventType.HandleDragDrop ? reader.ReadByte() : default;
                var target = new InventorySlot { InventoryId = reader.ReadInt32(), SlotIndex = reader.ReadInt32(), Count = type == ComponentInventoryPackage.EventType.HandleMoveItem ? reader.ReadInt32() : 0 };
                return new ComponentInventoryPackage(source, target, type)
                { DragMode = drag, ProcessingOnly = type == ComponentInventoryPackage.EventType.HandleDragDrop && reader.ReadBoolean() };
            case ComponentInventoryPackage.EventType.InventorySync:
                var count = reader.ReadCount(ProtocolFrameCodec.MaximumPayloadLength / 8); EnsureRemaining(reader, count * 8);
                var groups = new Dictionary<int, IReadOnlyList<InventorySlotSnapshot>>();
                for (var i = 0; i < count; i++)
                {
                    var id = reader.ReadInt32(); var length = reader.ReadCount(ProtocolFrameCodec.MaximumPayloadLength / 6); EnsureRemaining(reader, length * 6);
                    var slots = new InventorySlotSnapshot[length];
                    for (var j = 0; j < length; j++)
                    {
                        var index = reader.ReadInt32(); var kind = reader.ReadByte();
                        if (kind == 0) { slots[j] = new InventorySlotSnapshot { SlotIndex = index, Type = kind, Count = reader.ReadInt32(), Value = reader.ReadInt32() }; }
                        else if (kind == 1)
                        {
                            var clothes = new int[reader.ReadByte()]; EnsureRemaining(reader, clothes.Length * 4);
                            for (var k = 0; k < clothes.Length; k++) { clothes[k] = reader.ReadInt32(); }
                            slots[j] = new InventorySlotSnapshot { SlotIndex = index, Type = kind, ClothingList = clothes };
                        }
                        else { throw new InvalidDataException("Unknown inventory slot kind."); }
                    }
                    if (!groups.TryAdd(id, slots)) { throw new InvalidDataException("Duplicate inventory ID."); }
                }
                return new ComponentInventoryPackage { PackageEventType = type, Slots = groups };
            default: throw new InvalidDataException("Unknown inventory mode.");
        }
    }
}
