using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static SubsystemBodyPackage ReadBody(ProtocolReader reader)
    {
        var type = reader.ReadEnum<SubsystemBodyPackage.EventType>();
        switch (type)
        {
            case SubsystemBodyPackage.EventType.BodyUpdate:
                var tick = reader.ReadUInt32();
                var count = reader.ReadUInt16();
                EnsureRemaining(reader, count * 5);
                var items = new SubsystemBodyPackage.BodyItem[count];
                for (var i = 0; i < count; i++) { items[i] = ReadBodyItem(reader); }

                return new SubsystemBodyPackage { PackageEventType = type, StateTick = tick, BodyList = items };
            case SubsystemBodyPackage.EventType.ApplyImpulse:
                return new SubsystemBodyPackage
                    { PackageEventType = type, CreatureId = reader.ReadInt32(), Impulse = reader.ReadVector3() };
            case SubsystemBodyPackage.EventType.HandleAxisCollision:
                return new SubsystemBodyPackage
                {
                    PackageEventType = type, CreatureId = reader.ReadInt32(), TargetCreatureId = reader.ReadInt32(),
                    Impulse = reader.ReadVector3()
                };
            default: throw new InvalidDataException("Unknown body message mode.");
        }
    }

    private static void WriteBody(SubsystemBodyPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.PackageEventType);
        switch (message.PackageEventType)
        {
            case SubsystemBodyPackage.EventType.BodyUpdate:
                if (message.BodyList.Count > ushort.MaxValue) { throw new ArgumentException("Too many bodies."); }

                writer.Write(message.StateTick);
                writer.Write((ushort)message.BodyList.Count);
                foreach (var item in message.BodyList) { WriteBodyItem(writer, item); }

                break;
            case SubsystemBodyPackage.EventType.HandleAxisCollision:
                writer.Write(message.CreatureId);
                writer.Write(message.TargetCreatureId);
                writer.Write(message.Impulse);
                break;
            case SubsystemBodyPackage.EventType.ApplyImpulse:
                writer.Write(message.CreatureId);
                writer.Write(message.Impulse);
                break;
            default: throw new ArgumentException("Unknown body message mode.");
        }
    }

    private static SubsystemBodyPackage.BodyItem ReadBodyItem(ProtocolReader reader)
    {
        var bodyItem = new SubsystemBodyPackage.BodyItem
        {
            ChangeFlag = reader.ReadEnum<SubsystemBodyPackage.ChangeFlag>(),
            CreatureId = reader.ReadInt32()
        };

        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.PositionChange))
        {
            bodyItem.Position = reader.ReadVector3();
        }

        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.RotationChange))
        {
            bodyItem.Rotation = reader.ReadQuaternion();
        }

        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.VelocityChange))
        {
            bodyItem.Velocity = reader.ReadVector3();
        }

        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.LookAnglesChange))
        {
            bodyItem.LookAngles = reader.ReadVector2();
        }

        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.FlyOrderChange))
        {
            bodyItem.FlyOrder = reader.ReadVector3Nullable();
        }

        return bodyItem;
    }

    private static void WriteBodyItem(ProtocolWriter writer, SubsystemBodyPackage.BodyItem bodyItem)
    {
        writer.WriteEnum(bodyItem.ChangeFlag);
        writer.Write(bodyItem.CreatureId);
        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.PositionChange))
        {
            writer.Write(bodyItem.Position);
        }

        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.RotationChange))
        {
            writer.Write(bodyItem.Rotation);
        }

        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.VelocityChange))
        {
            writer.Write(bodyItem.Velocity);
        }

        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.LookAnglesChange))
        {
            writer.Write(bodyItem.LookAngles);
        }

        if (bodyItem.ChangeFlag.HasFlag(SubsystemBodyPackage.ChangeFlag.FlyOrderChange))
        {
            writer.Write(bodyItem.FlyOrder);
        }
    }
}
