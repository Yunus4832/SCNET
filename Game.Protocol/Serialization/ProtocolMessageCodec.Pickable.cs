using Game.Protocol.Entities;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WritePickablePackage(PickablePackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type); writer.Write(message.StateTick);
        switch (message.Type)
        {
            case PickablePackage.PickType.Create:
                writer.Write(message.Id); writer.Write(message.Count); writer.Write(message.Value);
                writer.Write(message.Position); writer.Write(message.Velocity); writer.Write(message.StuckMatrix);
                break;
            case PickablePackage.PickType.Update:
                writer.Write(message.Positions.Count);
                foreach (var p in message.Positions) { writer.Write(p.Id); writer.Write(p.Position); }
                break;
            case PickablePackage.PickType.CreateList:
                writer.Write(message.Pickables.Count);
                foreach (var p in message.Pickables)
                { writer.Write(p.Id); writer.Write(p.Count); writer.Write(p.Value); writer.Write(p.Position); writer.Write(p.Velocity); writer.Write(p.StuckMatrix); }
                break;
            case PickablePackage.PickType.DeleteList:
                writer.Write(message.RemovedIds.Count); foreach (var id in message.RemovedIds) { writer.Write(id); }
                break;
            case PickablePackage.PickType.Delete:
                writer.Write(message.Id); writer.Write(message.PlaySound);
                break;
            case PickablePackage.PickType.RequestSync:
                writer.Write(message.Id);
                break;
            default: throw new ArgumentException("Unknown pickable mode.");
        }
    }
    private static PickablePackage ReadPickablePackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<PickablePackage.PickType>(); var tick = reader.ReadUInt32();
        switch (type)
        {
            case PickablePackage.PickType.Create:
                return new PickablePackage { Type = type, StateTick = tick, Id = reader.ReadUInt16(), Count = reader.ReadByte(), Value = reader.ReadInt32(), Position = reader.ReadVector3(), Velocity = reader.ReadVector3(), StuckMatrix = reader.ReadMatrixNullable() };
            case PickablePackage.PickType.Delete:
                return new PickablePackage { Type = type, StateTick = tick, Id = reader.ReadUInt16(), PlaySound = reader.ReadBoolean() };
            case PickablePackage.PickType.RequestSync:
                return new PickablePackage { Type = type, StateTick = tick, Id = reader.ReadUInt16() };
            case PickablePackage.PickType.Update:
                var positions = new PickablePosition[ReadPickableCount(reader, 14)];
                for (var i = 0; i < positions.Length; i++) { positions[i] = new PickablePosition(reader.ReadUInt16(), reader.ReadVector3()); }
                return new PickablePackage { Type = type, StateTick = tick, Positions = positions };
            case PickablePackage.PickType.CreateList:
                var items = new PickableSnapshot[ReadPickableCount(reader, 35)];
                for (var i = 0; i < items.Length; i++) { items[i] = new PickableSnapshot(reader.ReadUInt16(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadVector3(), reader.ReadVector3(), reader.ReadMatrixNullable()); }
                return new PickablePackage { Type = type, StateTick = tick, Pickables = items };
            case PickablePackage.PickType.DeleteList:
                var ids = new ushort[ReadPickableCount(reader, 2)];
                for (var i = 0; i < ids.Length; i++) { ids[i] = reader.ReadUInt16(); }
                return new PickablePackage { Type = type, StateTick = tick, RemovedIds = ids };
            default: throw new InvalidDataException("Unknown pickable mode.");
        }
    }
    private static int ReadPickableCount(ProtocolReader reader, int minimumSize)
    {
        var count = reader.ReadCount(ProtocolFrameCodec.MaximumPayloadLength / minimumSize);
        EnsureRemaining(reader, count * minimumSize); return count;
    }
}
