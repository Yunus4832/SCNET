namespace Game.Network.Packages.Handlers;

public sealed class SubsystemTimePackageHandler : PackageHandlerBase<SubsystemTimePackage>
{
    public override void Handle(SubsystemTimePackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        if (!isServer)
        {
            var info = project.FindSubsystem<SubsystemGameInfo>(true)!;
            info.TotalElapsedGameTime = package.Time;
            info.TimeOfDay.TimeOfDayOffset = package.TimeOfDayOffset;
        }
    }
}
