namespace Game.Network.Packages.Handlers;

public sealed class SubsystemElectricityPackageHandler : PackageHandlerBase<SubsystemElectricityPackage>
{
    public override void Handle(SubsystemElectricityPackage package, PackageReceiveContext context)
    {
        if (context.IsServer || GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystem = project.FindSubsystem<SubsystemElectricity>(true)!;
        subsystem.ReceiveNetworkSnapshot(package.Snapshot);
    }
}
