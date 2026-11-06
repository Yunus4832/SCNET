using EntitySystem.Core;

namespace Game.World;

/// <summary>Owns transport publication for one authority project's core clock facts.</summary>
public sealed class WorldTimeReplication : IDisposable
{
    private readonly Project _project;

    private readonly SubsystemGameInfo _info;

    private readonly SubsystemSeasons? _seasons;

    public WorldTimeReplication(Project project)
    {
        _project = project;
        _info = project.FindSubsystem<SubsystemGameInfo>(true)!;
        _seasons = project.FindSubsystem<SubsystemSeasons>(false);
        _info.TimeSynchronizationRequested += PublishTime;
        if (_seasons is not null) { _seasons.TimeOfYearSynchronizationRequested += PublishSeason; }
    }

    private void PublishTime(WorldTimeSnapshot value)
    {
        if (WorldRuntimeContext.IsProjectActive(_project))
        {
            NetworkSender.Send(
                _project,
                new SubsystemTimePackage(value.TotalElapsedGameTime, value.TimeOfDayOffset),
                PackageAudience.Global
            );
        }
    }

    private void PublishSeason(float timeOfYear)
    {
        if (WorldRuntimeContext.IsProjectActive(_project))
        {
            NetworkSender.Send(_project, new SubsystemSeasonPackage(timeOfYear), PackageAudience.Global);
        }
    }

    public void Dispose()
    {
        _info.TimeSynchronizationRequested -= PublishTime;
        if (_seasons is not null) { _seasons.TimeOfYearSynchronizationRequested -= PublishSeason; }
    }
}
