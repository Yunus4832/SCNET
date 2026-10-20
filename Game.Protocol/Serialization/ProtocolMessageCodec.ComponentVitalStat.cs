using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteComponentVitalStatPackage(ComponentVitalStatPackage message, ProtocolWriter writer)
    {
        writer.Write(message.EntityId);
        writer.WriteEnum(message.PackageEventType);
        switch (message.PackageEventType)
        {
            case ComponentVitalStatPackage.EventType.SyncStat:
                writer.Write(message.Food);
                writer.Write(message.Sleep);
                writer.Write(message.Wetness);
                writer.Write(message.Temperature);
                writer.Write(message.Stamina);
                break;
        }
    }

    private static ComponentVitalStatPackage ReadComponentVitalStatPackage(ProtocolReader reader)
    {
        var entity = reader.ReadInt32();
        var type = reader.ReadEnum<ComponentVitalStatPackage.EventType>();
        return type switch
        {
            ComponentVitalStatPackage.EventType.SyncStat => new ComponentVitalStatPackage
            {
                EntityId = entity,
                PackageEventType = type,
                Food = reader.ReadSingle(),
                Sleep = reader.ReadSingle(),
                Wetness = reader.ReadSingle(),
                Temperature = reader.ReadSingle(),
                Stamina = reader.ReadSingle()
            },
            ComponentVitalStatPackage.EventType.RequestEat => new ComponentVitalStatPackage { EntityId = entity, PackageEventType = type },
            _ => throw new InvalidDataException("Unknown vital state message mode.")
        };
    }
}
