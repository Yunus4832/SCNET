namespace Game.Network.Handling;

public sealed class SubsystemSeasonPackageHandler : PackageHandlerBase<SubsystemSeasonPackage>
{
    public override void Handle(SubsystemSeasonPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (context.Project is null)
        {
            return;
        }

        if (isServer ||
            !float.IsFinite(package.TimeOfYear) ||
            package.TimeOfYear is < 0f or >= 1f)
        {
            return;
        }

        var seasons = context.Project.FindSubsystem<SubsystemSeasons>(true)!;
        seasons.SetTimeOfYear(package.TimeOfYear);
    }
}
