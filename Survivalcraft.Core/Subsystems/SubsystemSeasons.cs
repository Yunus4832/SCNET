using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemSeasons : Subsystem, IUpdateable
{
    public UpdateExecutionSide UpdateSide => UpdateExecutionSide.Authority;

    private SubsystemGameInfo _subsystemGameInfo = null!;

    public event Action<float>? TimeOfYearSynchronizationRequested;

    public Season Season { get; set; }

    public float TimeOfSeason { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        if (!WorldRuntimeContext.For(Project).IsAuthority) { return; }

        UpdateSeasonState();
        if (Time.PeriodicEvent(10, 0.0))
        {
            TimeOfYearSynchronizationRequested?.Invoke(_subsystemGameInfo.WorldSettings.TimeOfYear);
        }
    }

    public void SetTimeOfYear(float timeOfYear)
    {
        if (!float.IsFinite(timeOfYear)) { throw new ArgumentOutOfRangeException(nameof(timeOfYear)); }

        _subsystemGameInfo.WorldSettings.TimeOfYear = IntervalUtils.Normalize(timeOfYear);
        UpdateSeasonState();
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        _subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(true)!;
    }

    private void UpdateSeasonState()
    {
        SeasonCalendar.TimeOfYearToSeason(
            _subsystemGameInfo.WorldSettings.TimeOfYear,
            out var season,
            out var timeOfSeason
        );
        Season = season;
        TimeOfSeason = timeOfSeason;
    }
}
