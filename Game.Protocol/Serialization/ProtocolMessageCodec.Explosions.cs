using Engine.Core;

using Game.Protocol.World;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteExplosionsPackage(ExplosionsPackage message, ProtocolWriter writer)
    {
        writer.WriteEnum(message.Type);
        switch (message.Type)
        {
            case ExplosionsPackage.EventType.Cell:
                writer.Write(message.Cells.Count);
                foreach (var (chunk, cells) in message.Cells)
                {
                    writer.Write(chunk);
                    writer.Write(cells.Count);
                    foreach (var cell in cells)
                    {
                        writer.Write(cell.Position);
                        writer.Write(cell.Pressure);
                    }
                }

                break;
            case ExplosionsPackage.EventType.Sound:
                writer.Write(message.Position);
                writer.Write(message.Level);
                writer.Write(message.Delay);
                break;
            default: throw new ArgumentException("Unknown explosion message mode.");
        }
    }

    private static ExplosionsPackage ReadExplosionsPackage(ProtocolReader reader)
    {
        var type = reader.ReadEnum<ExplosionsPackage.EventType>();
        if (type == ExplosionsPackage.EventType.Sound)
        {
            return new ExplosionsPackage(reader.ReadVector3(), reader.ReadSingle(), reader.ReadSingle());
        }

        if (type != ExplosionsPackage.EventType.Cell)
        {
            throw new InvalidDataException("Unknown explosion message mode.");
        }

        var count = reader.ReadCount(ProtocolFrameCodec.MaximumPayloadLength / 12);
        EnsureRemaining(reader, count * 12);
        var chunks = new Dictionary<Point2, IReadOnlyList<ExplosionCell>>();
        for (var i = 0; i < count; i++)
        {
            var chunk = reader.ReadPoint2();
            var length = reader.ReadCount(ProtocolFrameCodec.MaximumPayloadLength / 16);
            EnsureRemaining(reader, length * 16);
            var cells = new ExplosionCell[length];
            for (var j = 0; j < length; j++) { cells[j] = new ExplosionCell(reader.ReadPoint3(), reader.ReadSingle()); }

            if (!chunks.TryAdd(chunk, cells)) { throw new InvalidDataException("Duplicate explosion chunk."); }
        }

        return new ExplosionsPackage { Type = type, Cells = chunks };
    }
}
