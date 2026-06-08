using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemElectricityPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        Subsystem = project.FindSubsystem<SubsystemElectricity>(true)!;
        Subsystem.List.AddRange(NetSimulates);
    }
}

public sealed class SubsystemElectricityPackageHandler : PackageHandlerBase<SubsystemElectricityPackage>
{
    public override void Handle(SubsystemElectricityPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(SubsystemElectricityPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
