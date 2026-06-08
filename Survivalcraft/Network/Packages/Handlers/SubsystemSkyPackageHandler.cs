namespace Game.Network.Packages.Handlers;

public sealed class SubsystemSkyPackageHandler : PackageHandlerBase<SubsystemSkyPackage>
{
    public override void Handle(SubsystemSkyPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode == GameMode.Creative || !isServer)
        {
            var subsystemSky = project.FindSubsystem<SubsystemSky>(true)!;
            if (package.IsRequest)
            {
                project.FindSubsystem<SubsystemWeather>(true)!.ManualLightingStrike(package.LightningStrikePosition,
                    package.Direction);
                subsystemSky.MakeLightningStrike(package.LightningStrikePosition);
            }
            else
            {
                subsystemSky.NetMakeLightingStrike(package.LightningStrikePosition);
            }
        }
        else
        {
            Log.Information($"{package.From?.PlayerData.Name} 打算在非创造模式下使用闪电");
        }
    }
}
