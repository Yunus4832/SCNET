namespace Game.Network.Packages.Handlers;

public sealed class SubsystemTimePackageHandler : PackageHandlerBase<SubsystemTimePackage>
{
    public override void Handle(SubsystemTimePackage package, NetNode? netNode, bool isServer)
    {
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
