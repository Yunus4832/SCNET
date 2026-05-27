using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

/// <summary>
/// 发射器
/// </summary>
public class DispenserPackage : IPackage
{
    private byte _flag;

    private Point3 _point;

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
        _point = point;
        _flag = flag;
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var sut = project.FindSubsystem<SubsystemTerrain>(true)!;
        var value = sut.Terrain.GetCellValue(_point.X, _point.Y, _point.Z);
        if (Terrain.ExtractContents(value) == DispenserBlock.Index)
        {
            var data = Terrain.ExtractData(value);
            data = DispenserBlock.SetMode(data,
                (_flag & 1) != 0 ? DispenserBlock.Mode.Shoot : DispenserBlock.Mode.Dispense);
            data = DispenserBlock.SetAcceptsDrops(data, (_flag & (1 << 1)) != 0);
            sut.ChangeCell(_point.X, _point.Y, _point.Z, Terrain.ReplaceData(value, data));
        }

        if (isServer)
        {
            netNode.QueuePackage(this);
        }
    }

    public void ReadData(PackageStreamReader reader)
    {
        _point = reader.ReadPoint3();
        _flag = reader.ReadByte();
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_point);
        writer.Write(_flag);
    }
}
