using Game.Protocol.Blocks;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteMovingBlockPackage(MovingBlockPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        writer.Write(message.NetworkId);
        writer.Write(message.StateTick);
        if (message.Type == MovingBlockPackage.EventType.PistonSound)
        {
            writer.Write(message.Position);
        }
        if (message.Type is MovingBlockPackage.EventType.Update or MovingBlockPackage.EventType.Stopped)
        {
            writer.Write(message.Position);
            writer.Write(message.Velocity);
            writer.Write(message.Speed);
        }
        if (message.Type == MovingBlockPackage.EventType.Add)
        {
            writer.WriteBuff(message.AddData ?? throw new ArgumentException("Missing moving block baseline."));
        }
    }

    private static MovingBlockPackage ReadMovingBlockPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<MovingBlockPackage.EventType>();
        var id = reader.ReadInt32();
        var tick = reader.ReadUInt32();
        return type switch
        {
            MovingBlockPackage.EventType.Add => new MovingBlockPackage { Type = type, NetworkId = id, StateTick = tick, AddData = reader.ReadBuff() },
            MovingBlockPackage.EventType.Remove => new MovingBlockPackage { Type = type, NetworkId = id, StateTick = tick },
            MovingBlockPackage.EventType.Update or MovingBlockPackage.EventType.Stopped => new MovingBlockPackage
            { Type = type, NetworkId = id, StateTick = tick, Position = reader.ReadVector3(), Velocity = reader.ReadVector3(), Speed = reader.ReadSingle() },
            MovingBlockPackage.EventType.PistonSound => new MovingBlockPackage { Type = type, NetworkId = id, StateTick = tick, Position = reader.ReadVector3() },
            _ => throw new InvalidDataException("Unknown moving block message mode.")
        };
    }
}
