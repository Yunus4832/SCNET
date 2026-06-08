using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
/// 发射器
/// </summary>
public class DispenserPackage : IPackage
{
    public byte Flag;

    public Point3 Point;

    public byte ID => (byte)PackageType.Dispenser;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public DispenserPackage()
    {
    }

    public DispenserPackage(Point3 point, byte flag)
    {
        Point = point;
        Flag = flag;
    }


    public void ReadData(PackageStreamReader reader)
    {
        Point = reader.ReadPoint3();
        Flag = reader.ReadByte();
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(Point);
        writer.Write(Flag);
    }
}
