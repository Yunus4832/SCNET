using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemTimePackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var info = project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (info.WorldSettings.GameMode == GameMode.Creative || !isServer)
        {
            info.TotalElapsedGameTime = Time;
            info.TimeOfDay.TimeOfDayOffset = TimeOfDayOffset;
        }
        else
        {
            if (From != null)
            {
                Log.Information($"{From.PlayerData.Name} 打算在非创造模式下修改时间");
            }
        }
    }
}

public sealed class SubsystemTimePackageHandler : PackageHandlerBase<SubsystemTimePackage>
{
    public override void Handle(SubsystemTimePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(SubsystemTimePackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
