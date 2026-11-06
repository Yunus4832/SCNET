namespace Game.Network.Handling;

public sealed class SubsystemSkyPackageHandler : PackageHandlerBase<SubsystemSkyPackage>
{
    public override void Handle(SubsystemSkyPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (context.Project is null)
        {
            return;
        }

        if (!isServer)
        {
            var project = context.Project;
            var subsystemSky = project.FindSubsystem<SubsystemSky>(true)!;
            subsystemSky.ApplyLightningStrike(package.LightningStrikePosition);
        }
    }
}
