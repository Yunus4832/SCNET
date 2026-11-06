namespace Game.Network.Handling;

public sealed class SubsystemWeatherPackageHandler : PackageHandlerBase<SubsystemWeatherPackage>
{
    public override void Handle(SubsystemWeatherPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (context.Project is null)
        {
            return;
        }

        var project = context.Project;
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
