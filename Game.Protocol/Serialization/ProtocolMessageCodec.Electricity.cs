using Engine.Core.Mathematics;

using Game.Protocol.Blocks;

namespace Game.Protocol.Serialization;

public static partial class ProtocolMessageCodec
{
    private static void WriteSubsystemElectricityPackage(SubsystemElectricityPackage message, ProtocolWriter writer)
    {
        writer.Write(message.Snapshot.StartStep);
        writer.Write(message.Snapshot.IsBaseline);
        writer.Write(message.Snapshot.SaveData.Count);
        foreach (var item in message.Snapshot.SaveData)
        {
            writer.WriteBlockPoint(item.Key);
            writer.Write(item.Value);
        }

        writer.Write(message.Snapshot.Removed.Count);
        foreach (var point in message.Snapshot.Removed)
        {
            writer.WriteBlockPoint(point);
        }
    }

    private static SubsystemElectricityPackage ReadSubsystemElectricityPackage(ProtocolReader reader)
    {
        var step = reader.ReadInt32();
        var baseline = reader.ReadBoolean();
        var data = new Dictionary<Point3, float>();
        var removed = new List<Point3>();
        var count = reader.ReadInt32();
        if (count < 0 || count > (reader.BaseStream.Length - reader.BaseStream.Position) / 16)
        {
            throw new InvalidDataException("Invalid electricity voltage count.");
        }

        for (var i = 0; i < count; i++)
        {
            if (!data.TryAdd(reader.ReadBlockPoint(), reader.ReadSingle()))
            {
                throw new InvalidDataException("Duplicate electricity cell.");
            }
        }

        var removedCount = reader.ReadInt32();
        if (removedCount < 0 || removedCount > (reader.BaseStream.Length - reader.BaseStream.Position) / 12)
        {
            throw new InvalidDataException("Invalid electricity removal count.");
        }

        for (var i = 0; i < removedCount; i++)
        {
            removed.Add(reader.ReadBlockPoint());
        }

        return new SubsystemElectricityPackage(
            new ElectricitySnapshot
                { StartStep = step, IsBaseline = baseline, SaveData = data, Removed = removed.ToArray() }
        );
    }
}
