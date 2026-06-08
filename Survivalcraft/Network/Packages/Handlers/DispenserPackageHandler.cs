using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class DispenserPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var sut = project.FindSubsystem<SubsystemTerrain>(true)!;
        var value = sut.Terrain.GetCellValue(Point.X, Point.Y, Point.Z);
        if (Terrain.ExtractContents(value) == DispenserBlock.Index)
        {
            var data = Terrain.ExtractData(value);
            data = DispenserBlock.SetMode(data,
                (Flag & 1) != 0 ? DispenserBlock.Mode.Shoot : DispenserBlock.Mode.Dispense);
            data = DispenserBlock.SetAcceptsDrops(data, (Flag & (1 << 1)) != 0);
            sut.ChangeCell(Point.X, Point.Y, Point.Z, Terrain.ReplaceData(value, data));
        }

        if (isServer)
        {
            netNode.QueuePackage(this);
        }
    }
}

public sealed class DispenserPackageHandler : PackageHandlerBase<DispenserPackage>
{
    public override void Handle(DispenserPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(DispenserPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
