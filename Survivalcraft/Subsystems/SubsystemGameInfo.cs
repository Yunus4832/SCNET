using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Subsystems;

public class SubsystemGameInfo : Subsystem, IUpdateable
{
    private GameMode? _persistedGameMode;

    private double? _lastTotalElapsedGameTime;

    private SubsystemTime _subsystemTime = null!;

    public SubsystemTimeOfDay TimeOfDay = null!;

    public WorldSettings WorldSettings { get; set; } = null!;

    public string DirectoryName { get; set; } = string.Empty;

    public double TotalElapsedGameTime { get; set; }

    public float TotalElapsedGameTimeDelta { get; set; }

    public int WorldSeed { get; set; }

    public bool ServerAdministrationClaimed { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        TotalElapsedGameTime += dt;
        TotalElapsedGameTimeDelta = _lastTotalElapsedGameTime.HasValue
            ? (float)(TotalElapsedGameTime - _lastTotalElapsedGameTime.Value)
            : 0f;
        _lastTotalElapsedGameTime = TotalElapsedGameTime;
        if (WorldSettings.AreSeasonsChanging && _subsystemTime.PeriodicGameTimeEvent(10.0, 5.0))
        {
            var num = WorldSettings.YearDays * 1200f;
            WorldSettings.TimeOfYear = IntervalUtils.Normalize(WorldSettings.TimeOfYear + 10f / num);
        }

        //客户端禁用时间计算
        if (CommonLib.WorkType == WorkType.Client)
        {
            return;
        }

        if (_subsystemTime.PeriodicGameTimeEvent(1.0, 0.0))
        {
            CommonLib.Net.QueuePackage(new SubsystemTimePackage(TotalElapsedGameTime, TimeOfDay.TimeOfDayOffset));
        }
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        TimeOfDay = Project.FindSubsystem<SubsystemTimeOfDay>(true)!;
        _subsystemTime = Project.FindSubsystem<SubsystemTime>(true)!;
        WorldSettings = new WorldSettings();
        WorldSettings.Load(valuesDictionary);
        DirectoryName = valuesDictionary.GetValue<string>("WorldDirectoryName");
        TotalElapsedGameTime = valuesDictionary.GetValue<double>("TotalElapsedGameTime");
        WorldSeed = valuesDictionary.GetValue<int>("WorldSeed");
        ServerAdministrationClaimed = valuesDictionary.GetValue("ServerAdministrationClaimed", false);
    }

    public override void Save(ValuesDictionary valuesDictionary)
    {
        WorldSettings.Save(valuesDictionary, false);
        if (_persistedGameMode is { } persistedGameMode)
        {
            valuesDictionary.SetValue("GameMode", persistedGameMode);
        }

        valuesDictionary.SetValue("WorldSeed", WorldSeed);
        valuesDictionary.SetValue("TotalElapsedGameTime", TotalElapsedGameTime);
        valuesDictionary.SetValue("ServerAdministrationClaimed", ServerAdministrationClaimed);
    }

    public void ApplyGameModeOverride(GameMode gameMode)
    {
        _persistedGameMode ??= WorldSettings.GameMode;
        WorldSettings.GameMode = gameMode;
    }
}
