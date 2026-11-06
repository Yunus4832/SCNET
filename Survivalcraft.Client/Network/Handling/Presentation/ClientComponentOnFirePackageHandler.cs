using Game.Network.Transport;

namespace Game.Network.Handling.Presentation;

public sealed class ClientComponentOnFirePackageHandler : ComponentOnFirePackageHandler
{
    public override void Handle(ComponentOnFirePackage package, PackageReceiveContext context)
    {
        base.Handle(package, context);
        if (context.IsServer || !context.Runtime.HasPresentation || context.Project is not { } project) { return; }

        if (package is BlockBurnEffectPackage effect)
        {
            project.FindSubsystem<SubsystemFirePresentation>(true)!.PlayBurnEffect(effect.Position, effect.Kind);
        }
    }
}
