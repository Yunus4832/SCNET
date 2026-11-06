using Game.Network.Transport;

namespace Game.Network.Handling.Presentation;

public sealed class ClientComponentVitalStatPackageHandler : ComponentVitalStatPackageHandler
{
    public override void Handle(ComponentVitalStatPackage package, PackageReceiveContext context)
    {
        base.Handle(package, context);
        if (context.IsServer || !context.Runtime.HasPresentation || context.Project is not { } project) { return; }

        if (package is VitalFeedbackPackage feedback)
        {
            project.FindEntityById(
                feedback.EntityId,
                entity =>
                    entity.FindComponent<ComponentVitalStatsPresentation>()
                        ?.ApplyFeedback(feedback.MessageKey, feedback.IsEating)
            );
        }
    }
}
