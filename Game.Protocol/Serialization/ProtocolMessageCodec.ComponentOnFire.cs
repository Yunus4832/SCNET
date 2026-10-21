using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteComponentOnFirePackage(ComponentOnFirePackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        switch (message.Type)
        {
            case ComponentOnFirePackage.EventType.ComponentOnFire:
                writer.Write(message.EntityId);
                writer.Write(message.AttackerEntityId);
                writer.Write(message.Duration);
                break;
            case ComponentOnFirePackage.EventType.BlockOnFireAdd:
                writer.Write(message.X);
                writer.Write(message.Y);
                writer.Write(message.Z);
                writer.Write(message.Duration);
                break;
            case ComponentOnFirePackage.EventType.BlockOnFireRemove:
                writer.Write(message.X);
                writer.Write(message.Y);
                writer.Write(message.Z);
                break;
        }
    }

    private static ComponentOnFirePackage ReadComponentOnFirePackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<ComponentOnFirePackage.EventType>();
        return type switch
        {
            ComponentOnFirePackage.EventType.ComponentOnFire => new ComponentOnFirePackage
            {
                Type = type, EntityId = reader.ReadInt32(), AttackerEntityId = reader.ReadInt32(),
                Duration = reader.ReadSingle()
            },
            ComponentOnFirePackage.EventType.BlockOnFireAdd => new ComponentOnFirePackage(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadSingle()
            ),
            ComponentOnFirePackage.EventType.BlockOnFireRemove => new ComponentOnFirePackage(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32()
            ),
            _ => throw new InvalidDataException("Unknown fire message mode.")
        };
    }
}
