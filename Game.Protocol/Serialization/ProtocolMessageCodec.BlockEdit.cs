using Game.Protocol.Blocks;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteBlockEditPackage(BlockEditPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        switch (message.Type)
        {
            case BlockEditPackage.EventType.OpenInventoryByID:
                writer.Write(message.InventoryId);
                break;
            case BlockEditPackage.EventType.OpenInventoryByPoint:
                writer.Write(message.Point3);
                break;
            case BlockEditPackage.EventType.CrossbowPull:
                writer.Write(message.InventoryId);
                writer.Write(message.SlotIndex);
                break;
            case BlockEditPackage.EventType.EditSign:
                writer.Write(message.Point3);
                break;
        }
    }

    private static BlockEditPackage ReadBlockEditPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<BlockEditPackage.EventType>();
        return type switch
        {
            BlockEditPackage.EventType.OpenInventoryByID => new BlockEditPackage
                { Type = type, InventoryId = reader.ReadInt32() },
            BlockEditPackage.EventType.OpenInventoryByPoint or BlockEditPackage.EventType.EditSign =>
                new BlockEditPackage(reader.ReadPoint3(), type),
            BlockEditPackage.EventType.CrossbowPull => new BlockEditPackage
                { Type = type, InventoryId = reader.ReadInt32(), SlotIndex = reader.ReadInt32() },
            _ => throw new InvalidDataException("Unknown block edit mode.")
        };
    }
}
