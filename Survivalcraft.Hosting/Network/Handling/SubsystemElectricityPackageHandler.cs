namespace Game.Network.Handling;

public sealed class SubsystemElectricityPackageHandler : PackageHandlerBase<SubsystemElectricityPackage>
{
    public override void Handle(SubsystemElectricityPackage package, PackageReceiveContext context)
    {
        if (context.IsServer || context.Project is null)
        {
            return;
        }

        var project = context.Project;
        var subsystem = project.FindSubsystem<SubsystemElectricity>(true)!;
        subsystem.ReceiveNetworkSnapshot(package.Snapshot);
    }
}
