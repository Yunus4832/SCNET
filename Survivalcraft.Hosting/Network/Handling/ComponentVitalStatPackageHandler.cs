namespace Game.Network.Handling;

public class ComponentVitalStatPackageHandler : PackageHandlerBase<ComponentVitalStatPackage>
{
    public override void Handle(ComponentVitalStatPackage package, PackageReceiveContext context)
    {
        if (context.IsServer || context.Project is not { } project || package is not VitalStatePackage state)
        {
            return;
        }

        project.FindEntityById(
            state.EntityId,
            entity => entity.FindComponent<ComponentVitalStats>()?.ApplyState(state)
        );
    }
}
