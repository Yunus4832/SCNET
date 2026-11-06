using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class DispenserSoundPackageHandler : PackageHandlerBase<DispenserSoundPackage>
{
    public override void Handle(DispenserSoundPackage package, PackageReceiveContext context)
    {
        if (context.IsServer ||
            context.Project is not { } project ||
            !WorldRuntimeContext.IsProjectActive(project) ||
            !context.Runtime.HasPresentation) { return; }

        project.FindSubsystem<SubsystemSpatialSoundRequests>(true)!.RequestSound(
            package.Shot ? "Audio/DispenserShoot" : "Audio/DispenserDispense",
            1f,
            0f,
            package.Position,
            package.Shot ? 4f : 3f,
            true
        );
    }
}
