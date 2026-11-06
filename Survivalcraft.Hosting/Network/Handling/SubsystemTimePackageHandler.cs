namespace Game.Network.Handling;

public sealed class SubsystemTimePackageHandler : PackageHandlerBase<SubsystemTimePackage>
{
    public override void Handle(SubsystemTimePackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (context.Project is null)
        {
            return;
        }

        var project = context.Project;
        if (!isServer)
        {
            var info = project.FindSubsystem<SubsystemGameInfo>(true)!;
            info.TotalElapsedGameTime = package.Time;
            info.TimeOfDay.TimeOfDayOffset = package.TimeOfDayOffset;
        }
    }
}
