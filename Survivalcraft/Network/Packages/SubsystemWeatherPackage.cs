using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class SubsystemWeatherPackage : IPackage
{
    public float FogProgress;

    public int FogType = 0;

    public double FogEndTime;

    public float FogRampTime;

    public double FogStartTime;

    public float Intensity;

    public float LightningIntensity;

    public double PrecipitationEndTime;

    public double PrecipitationStartTime;

    public int WeatherType;

    public byte ID => (byte)PackageType.SubsystemWeather;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.ProjectLoaded;

    public SubsystemWeatherPackage()
    {
    }

    public SubsystemWeatherPackage(int weatherType)
    {
        WeatherType = weatherType;
    }

    public SubsystemWeatherPackage(float intensity)
    {
        Intensity = intensity;
    }

    public SubsystemWeatherPackage(double start, double end, float light)
    {
        PrecipitationEndTime = end;
        PrecipitationStartTime = start;
        LightningIntensity = light;
    }

    public SubsystemWeatherPackage(double fogStart, float ramp, double fogEnd, float progress)
    {
        FogStartTime = fogStart;
        FogRampTime = ramp;
        FogEndTime = fogEnd;
        FogProgress = progress;
    }

    public SubsystemWeatherPackage(double fogStart, float ramp, double fogEnd)
    {
        FogStartTime = fogStart;
        FogRampTime = ramp;
        FogEndTime = fogEnd;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(PrecipitationStartTime);
        writer.Write(PrecipitationEndTime);
        writer.Write(LightningIntensity);
        writer.Write(WeatherType);
        writer.Write(FogStartTime);
        writer.Write(FogRampTime);
        writer.Write(FogEndTime);
    }

    public void ReadData(PackageStreamReader reader)
    {
        PrecipitationStartTime = reader.ReadDouble();
        PrecipitationEndTime = reader.ReadDouble();
        LightningIntensity = reader.ReadSingle();
        WeatherType = reader.ReadInt32();

        FogStartTime = reader.ReadDouble();
        FogRampTime = reader.ReadSingle();
        FogEndTime = reader.ReadDouble();
    }


}
