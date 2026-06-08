namespace Game.Network.Packages.Handlers;

public sealed class SubsystemWeatherPackageHandler : PackageHandlerBase<SubsystemWeatherPackage>
{
    public override void Handle(SubsystemWeatherPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var weather = project.FindSubsystem<SubsystemWeather>(true)!;
        if (package.WeatherType != 0)
        {
            switch (package.WeatherType)
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

        weather.PrecipitationStartTime = package.PrecipitationStartTime;
        weather.PrecipitationEndTime = package.PrecipitationEndTime;
        weather.LightningIntensity = package.LightningIntensity;

        weather.FogStartTime = package.FogStartTime;
        weather.FogEndTime = package.FogEndTime;
        weather.FogRampTime = package.FogRampTime;
    }
}
