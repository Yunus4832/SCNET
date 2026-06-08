using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemWeatherPackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var weather = project.FindSubsystem<SubsystemWeather>(true)!;
        if (WeatherType != 0)
        {
            switch (WeatherType)
            {
                case 1:
                    weather.ManualPrecipitationEnd();
                    break;
                case 2:
                    weather.ManualPrecipitationStart();
                    break;
                case 3:
                    weather.ManualFogEnd();
                    break;
                case 4:
                    weather.ManualFogStart();
                    break;
            }

            return;
        }

        weather.PrecipitationStartTime = PrecipitationStartTime;
        weather.PrecipitationEndTime = PrecipitationEndTime;
        weather.LightningIntensity = LightningIntensity;

        weather.FogStartTime = FogStartTime;
        weather.FogEndTime = FogEndTime;
        weather.FogRampTime = FogRampTime;
    }
}

public sealed class SubsystemWeatherPackageHandler : PackageHandlerBase<SubsystemWeatherPackage>
{
    public override void Handle(SubsystemWeatherPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(SubsystemWeatherPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
