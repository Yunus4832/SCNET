namespace Game.Network.Handling;

public sealed class ComponentSicknessPackageHandler : PackageHandlerBase<ComponentSicknessPackage>
{
    public override void Handle(ComponentSicknessPackage package, PackageReceiveContext context)
    {
        if (context.Project is null || context.IsServer)
        {
            return;
        }

        var project = context.Project;
        project.FindEntityById(
            package.EntityId,
            entity =>
            {
                var sickness = entity.FindComponent<ComponentSickness>();
                if (sickness == null)
                {
                    return;
                }

                sickness.ApplyState(
                    package.SicknessDuration,
                    package.EffectSequence,
                    package.PukeDuration,
                    package.NauseaDuration,
                    package.IsNausea
                );
            }
        );
    }
}
