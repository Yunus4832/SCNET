using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemElectricityPackage : IPackage
{
    public readonly SubsystemElectricity.NetSimulate Snapshot = new();

    public byte ID => (byte)PackageType.SubsystemElectricity;




    public ClientState MinNeedState => ClientState.Playing;

    public SubsystemElectricityPackage()
    {
    }

    public SubsystemElectricityPackage(SubsystemElectricity.NetSimulate snapshot)
    {
        Snapshot = snapshot;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Snapshot.StartStep);
        writer.Write(Snapshot.IsBaseline);
        writer.Write(Snapshot.SaveData.Count);
        foreach (var item in Snapshot.SaveData)
        {
            writer.WriteBlockPoint(item.Key);
            writer.Write(item.Value);
        }

        writer.Write(Snapshot.Removed.Count);
        foreach (var point in Snapshot.Removed)
        {
            writer.WriteBlockPoint(point);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        Snapshot.StartStep = reader.ReadInt32();
        Snapshot.IsBaseline = reader.ReadBoolean();
        var count = reader.ReadInt32();
        if (count < 0 || count > (reader.BaseStream.Length - reader.BaseStream.Position) / 16)
        {
            throw new InvalidDataException("Invalid electricity voltage count.");
        }

        for (var i = 0; i < count; i++)
        {
            Snapshot.SaveData.Add(reader.ReadBlockPoint(), reader.ReadSingle());
        }

        var removedCount = reader.ReadInt32();
        if (removedCount < 0 || removedCount > (reader.BaseStream.Length - reader.BaseStream.Position) / 12)
        {
            throw new InvalidDataException("Invalid electricity removal count.");
        }

        for (var i = 0; i < removedCount; i++)
        {
            Snapshot.Removed.Add(reader.ReadBlockPoint());
        }
    }
}
