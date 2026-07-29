using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemWeatherPackage : IPackage
{
    public double FogEndTime;

    public float FogRampTime;

    public double FogStartTime;

    public float LightningIntensity;

    public double PrecipitationEndTime;

    public double PrecipitationStartTime;

    public byte ID => (byte)PackageType.SubsystemWeather;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public SubsystemWeatherPackage()
    {
    }

    public static SubsystemWeatherPackage CreateSnapshot(SubsystemWeather weather)
    {
        ArgumentNullException.ThrowIfNull(weather);
        return new SubsystemWeatherPackage
        {
            PrecipitationStartTime = weather.PrecipitationStartTime,
            PrecipitationEndTime = weather.PrecipitationEndTime,
            LightningIntensity = weather.LightningIntensity,
            FogStartTime = weather.FogStartTime,
            FogRampTime = weather.FogRampTime,
            FogEndTime = weather.FogEndTime
        };
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(PrecipitationStartTime);
        writer.Write(PrecipitationEndTime);
        writer.Write(LightningIntensity);
        writer.Write(FogStartTime);
        writer.Write(FogRampTime);
        writer.Write(FogEndTime);
    }

    public void ReadData(PackageStreamReader reader)
    {
        PrecipitationStartTime = reader.ReadDouble();
        PrecipitationEndTime = reader.ReadDouble();
        LightningIntensity = reader.ReadSingle();
        FogStartTime = reader.ReadDouble();
        FogRampTime = reader.ReadSingle();
        FogEndTime = reader.ReadDouble();
    }


}
