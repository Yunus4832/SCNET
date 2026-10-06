namespace Game.Network.Packages.Handlers;

public sealed class SubsystemSkyPackageHandler : PackageHandlerBase<SubsystemSkyPackage>
{
    public override void Handle(SubsystemSkyPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (GameManager.Project is null)
        {
            return;
        }

        if (!isServer)
        {
            var project = GameManager.Project;
            var subsystemSky = project.FindSubsystem<SubsystemSky>(true)!;
            subsystemSky.NetMakeLightingStrike(package.LightningStrikePosition);
        }
    }
}
