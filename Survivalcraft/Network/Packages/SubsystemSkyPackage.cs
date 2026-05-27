using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemSkyPackage : IPackage
{
    private bool _isRequest;

    public Vector3 LightningStrikePosition;

    private Vector3 _direction;

    public byte ID => (byte)PackageType.SubsystemSky;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;

    public SubsystemSkyPackage()
    {
    }

    public SubsystemSkyPackage(Vector3 position)
    {
        LightningStrikePosition = position;
    }

    public SubsystemSkyPackage(Vector3 position, Vector3 direction)
    {
        LightningStrikePosition = position;
        _direction = direction;
        _isRequest = true;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(LightningStrikePosition);
        writer.Write(_isRequest);
        writer.Write(_direction);
    }

    public void ReadData(PackageStreamReader reader)
    {
        LightningStrikePosition = reader.ReadVector3();
        _isRequest = reader.ReadBoolean();
        _direction = reader.ReadVector3();
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        if (project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.GameMode == GameMode.Creative || !isServer)
        {
            var subsystemSky = project.FindSubsystem<SubsystemSky>(true)!;
            if (_isRequest)
            {
                project.FindSubsystem<SubsystemWeather>(true)!.ManualLightingStrike(LightningStrikePosition, _direction);
                subsystemSky.MakeLightningStrike(LightningStrikePosition);
            }
            else
            {
                subsystemSky.NetMakeLightingStrike(LightningStrikePosition);
            }
        }
        else
        {
            Log.Information($"{From?.PlayerData.Name} 打算在非创造模式下使用闪电");
        }
    }
}
