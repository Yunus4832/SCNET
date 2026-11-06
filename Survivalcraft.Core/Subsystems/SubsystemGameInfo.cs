using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemGameInfo : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Both;

    private double? _lastTotalElapsedGameTime;

    private SubsystemTime _subsystemTime = null!;

    public SubsystemTimeOfDay TimeOfDay = null!;

    public WorldSettings WorldSettings { get; set; } = null!;

    public string DirectoryName { get; set; } = string.Empty;

    public double TotalElapsedGameTime { get; set; }

    public float TotalElapsedGameTimeDelta { get; set; }

    public int WorldSeed { get; set; }

    public bool ServerAdministrationClaimed { get; set; }

    public event Action<WorldTimeSnapshot>? TimeSynchronizationRequested;

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
        if (!WorldRuntimeContext.For(Project).IsAuthority)
        {
            return;
        }

        if (WorldRuntimeContext.For(Project).IsAuthority && _subsystemTime.PeriodicGameTimeEvent(1.0, 0.0))
        {
            TimeSynchronizationRequested?.Invoke(
                new WorldTimeSnapshot(TotalElapsedGameTime, TimeOfDay.TimeOfDayOffset)
            );
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
        valuesDictionary.SetValue("WorldSeed", WorldSeed);
        valuesDictionary.SetValue("TotalElapsedGameTime", TotalElapsedGameTime);
        valuesDictionary.SetValue("ServerAdministrationClaimed", ServerAdministrationClaimed);
    }

    public override void Dispose() => TimeSynchronizationRequested = null;
}
