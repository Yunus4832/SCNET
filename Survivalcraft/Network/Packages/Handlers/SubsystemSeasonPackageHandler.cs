namespace Game.Network.Packages.Handlers;

public sealed class SubsystemSeasonPackageHandler : PackageHandlerBase<SubsystemSeasonPackage>
{
    public override void Handle(SubsystemSeasonPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var weather = project.FindSubsystem<SubsystemSeasons>(true)!;
        weather.Season = (Season)package.SeasonIndexNet;
        weather.TimeOfSeason = package.TimeOfSeasonNet;
    }
}
