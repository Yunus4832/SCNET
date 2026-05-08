namespace Game.NetWork.Packages;

public class SubsystemWeatherPackage : IPackage
{
    public float FogProgress;

    public int FogType = 0;

    private double _fogEndTime;

    private float _fogRampTime;

    private double _fogStartTime;

    private float _intensity;

    private float _lightningIntensity;

    private double _precipitationEndTime;

    private double _precipitationStartTime;

    private int _weatherType;

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
        _weatherType = weatherType;
    }

    public SubsystemWeatherPackage(float intensity)
    {
        _intensity = intensity;
    }

    public SubsystemWeatherPackage(double start, double end, float light)
    {
        _precipitationEndTime = end;
        _precipitationStartTime = start;
        _lightningIntensity = light;
    }

    public SubsystemWeatherPackage(double fogStart, float ramp, double fogEnd, float progress)
    {
        _fogStartTime = fogStart;
        _fogRampTime = ramp;
        _fogEndTime = fogEnd;
        FogProgress = progress;
    }

    public SubsystemWeatherPackage(double fogStart, float ramp, double fogEnd)
    {
        _fogStartTime = fogStart;
        _fogRampTime = ramp;
        _fogEndTime = fogEnd;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_precipitationStartTime);
        writer.Write(_precipitationEndTime);
        writer.Write(_lightningIntensity);
        writer.Write(_weatherType);
        writer.Write(_fogStartTime);
        writer.Write(_fogRampTime);
        writer.Write(_fogEndTime);
    }

    public void ReadData(PackageStreamReader reader)
    {
        _precipitationStartTime = reader.ReadDouble();
        _precipitationEndTime = reader.ReadDouble();
        _lightningIntensity = reader.ReadSingle();
        _weatherType = reader.ReadInt32();

        _fogStartTime = reader.ReadDouble();
        _fogRampTime = reader.ReadSingle();
        _fogEndTime = reader.ReadDouble();
    }

    public void Handle(ProjectNet projectNet, NetNode netNode, bool isServer)
    {
        var weather = projectNet.FindSubsystem<SubsystemWeather>(true)!;
        if (_weatherType != 0)
        {
            switch (_weatherType)
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

        weather.PrecipitationStartTime = _precipitationStartTime;
        weather.PrecipitationEndTime = _precipitationEndTime;
        weather.LightningIntensity = _lightningIntensity;

        weather.FogStartTime = _fogStartTime;
        weather.FogEndTime = _fogEndTime;
        weather.FogRampTime = _fogRampTime;
    }
}
