using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteComponentBehaviorPackage(ComponentBehaviorPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.PackageEventType);
        writer.Write(message.EntityId);
        switch (message.PackageEventType)
        {
            case ComponentBehaviorPackage.EventType.FishOutOfWater:
            case ComponentBehaviorPackage.EventType.DigInMud:
            case ComponentBehaviorPackage.EventType.ChaseBehavior:
            case ComponentBehaviorPackage.EventType.RandomFeed:
            case ComponentBehaviorPackage.EventType.RandomPeck:
            case ComponentBehaviorPackage.EventType.EatPickable:
                writer.Write(message.RowLeft);
                break;
            case ComponentBehaviorPackage.EventType.HumanRow:
                writer.Write(message.RowLeft);
                writer.Write(message.RowRight);
                break;
            case ComponentBehaviorPackage.EventType.CreatureSound:
                writer.Write(message.Type);
                writer.Write(message.RowLeft);
                break;
        }
    }

    private static ComponentBehaviorPackage ReadComponentBehaviorPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<ComponentBehaviorPackage.EventType>();
        if (!Enum.IsDefined(type)) { throw new InvalidDataException("Unknown behavior message mode."); }

        var entity = reader.ReadInt32();
        return type switch
        {
            ComponentBehaviorPackage.EventType.CreatureSound => new ComponentBehaviorPackage
            {
                PackageEventType = type, EntityId = entity, Type = reader.ReadByte(), RowLeft = reader.ReadBoolean()
            },
            ComponentBehaviorPackage.EventType.HumanRow => new ComponentBehaviorPackage
            {
                PackageEventType = type, EntityId = entity, RowLeft = reader.ReadBoolean(),
                RowRight = reader.ReadBoolean()
            },
            _ => new ComponentBehaviorPackage
                { PackageEventType = type, EntityId = entity, RowLeft = reader.ReadBoolean() }
        };
    }
}
