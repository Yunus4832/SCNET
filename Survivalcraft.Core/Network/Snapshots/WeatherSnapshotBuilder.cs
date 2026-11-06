namespace Game.Network.Snapshots;

public static class WeatherSnapshotBuilder
{
    public static SubsystemWeatherPackage Capture(SubsystemWeather weather) => new()
    {
        PrecipitationStartTime = weather.PrecipitationStartTime,
        PrecipitationEndTime = weather.PrecipitationEndTime,
        LightningIntensity = weather.LightningIntensity,
        FogStartTime = weather.FogStartTime,
        FogRampTime = weather.FogRampTime,
        FogEndTime = weather.FogEndTime
    };
}
