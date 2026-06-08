namespace Game.Network.Packages.Handlers;

public sealed class SubsystemElectricityPackageHandler : PackageHandlerBase<SubsystemElectricityPackage>
{
    public override void Handle(SubsystemElectricityPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        package.Subsystem = project.FindSubsystem<SubsystemElectricity>(true)!;
        package.Subsystem.List.AddRange(package.NetSimulates);
    }
}
