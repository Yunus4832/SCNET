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
        if (isServer)
        {
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
