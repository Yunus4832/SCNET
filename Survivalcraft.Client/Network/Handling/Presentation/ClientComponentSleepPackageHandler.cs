using Game.Network.Transport;

namespace Game.Network.Handling.Presentation;

public sealed class ClientComponentSleepPackageHandler : ComponentSleepPackageHandler
{
    public override void Handle(ComponentSleepPackage package, PackageReceiveContext context)
    {
        base.Handle(package, context);
        if (context.Node is null ||
            context.IsServer ||
            context.Runtime.IsClosed ||
            !context.Runtime.HasPresentation ||
            context.Project is not { } project ||
            package is not SleepResultPackage { Result: false } failure) { return; }

        project.FindEntityById(
            failure.EntityId,
            entity =>
                entity.FindComponent<ComponentSleep>()?.Entity.FindComponent<ComponentSleepPresentation>()?
                    .ShowFailure(failure.Reason)
        );
    }
}
