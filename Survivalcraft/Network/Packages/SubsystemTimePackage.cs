using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public class SubsystemTimePackage : IPackage
{
    private double _time;

    private double _timeOfDayOffset;

    public byte ID => (byte)PackageType.SubsystemTime;

    public Client? To { get; set; }

    public Client? Except { get; set; }

    public Client? From { get; set; }

    public ClientState MinNeedState => ClientState.Playing;


    public SubsystemTimePackage()
    {
    }

    public SubsystemTimePackage(double totalElapsedGameTime, double timeOfDayOffset)
    {
        _time = totalElapsedGameTime;
        _timeOfDayOffset = timeOfDayOffset;
    }

    public void WriteData(PackageStreamWriter writer)
    {
        writer.Write(_time);
        writer.Write(_timeOfDayOffset);
    }

    public void ReadData(PackageStreamReader reader)
    {
        _time = reader.ReadDouble();
        _timeOfDayOffset = reader.ReadDouble();
    }

    public void Handle(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var info = project.FindSubsystem<SubsystemGameInfo>(true)!;
        if (info.WorldSettings.GameMode == GameMode.Creative || !isServer)
        {
            info.TotalElapsedGameTime = _time;
            info.TimeOfDay.TimeOfDayOffset = _timeOfDayOffset;
        }
        else
        {
            if (From != null)
            {
                Log.Information($"{From.PlayerData.Name} 打算在非创造模式下修改时间");
            }
        }
    }
}
