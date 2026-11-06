using Game.Network.Transport;

namespace Game.Network.Handling.Presentation;

public sealed class ClientComponentHealthPackageHandler : ComponentHealthPackageHandler
{
    public override void Handle(ComponentHealthPackage package, PackageReceiveContext context)
    {
        base.Handle(package, context);
        if (context.IsServer || !context.Runtime.HasPresentation || context.Project is not { } project) { return; }

        if (package is CharacterHitEffectPackage effect)
        {
            ComponentHealthPresentation.PlayHitEffect(project, effect);
        }
    }
}
