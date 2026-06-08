using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemSkyPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode == GameMode.Creative || !isServer)
        {
            var subsystemSky = project.FindSubsystem<SubsystemSky>(true)!;
            if (IsRequest)
            {
                project.FindSubsystem<SubsystemWeather>(true)!.ManualLightingStrike(LightningStrikePosition, Direction);
                subsystemSky.MakeLightningStrike(LightningStrikePosition);
            }
            else
            {
                subsystemSky.NetMakeLightingStrike(LightningStrikePosition);
            }
        }
        else
        {
            Log.Information($"{From?.PlayerData.Name} 打算在非创造模式下使用闪电");
        }
    }
}

public sealed class SubsystemSkyPackageHandler : PackageHandlerBase<SubsystemSkyPackage>
{
    public override void Handle(SubsystemSkyPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(SubsystemSkyPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
