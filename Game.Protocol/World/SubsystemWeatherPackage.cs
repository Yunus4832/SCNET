namespace Game.Protocol.World;

public sealed record SubsystemWeatherPackage : IProtocolMessage
{
    public double FogEndTime { get; init; }
    public float FogRampTime { get; init; }
    public double FogStartTime { get; init; }
    public float LightningIntensity { get; init; }
    public double PrecipitationEndTime { get; init; }
    public double PrecipitationStartTime { get; init; }
    public byte ID => (byte)PackageType.SubsystemWeather;
}
