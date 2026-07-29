namespace Game.Network.Packages.Handlers;

public sealed class SubsystemSeasonPackageHandler : PackageHandlerBase<SubsystemSeasonPackage>
{
    public override void Handle(SubsystemSeasonPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        if (isServer ||
            !Enum.IsDefined(typeof(Season), package.SeasonIndexNet) ||
            !float.IsFinite(package.TimeOfSeasonNet) ||
            package.TimeOfSeasonNet is < 0f or > 1f)
        {
            return;
        }

        var seasons = GameManager.Project.FindSubsystem<SubsystemSeasons>(true)!;
        seasons.Season = (Season)package.SeasonIndexNet;
        seasons.TimeOfSeason = package.TimeOfSeasonNet;
    }
}
