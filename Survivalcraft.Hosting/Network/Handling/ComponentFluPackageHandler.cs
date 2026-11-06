namespace Game.Network.Handling;

public sealed class ComponentFluPackageHandler : PackageHandlerBase<ComponentFluPackage>
{
    public override void Handle(ComponentFluPackage package, PackageReceiveContext context)
    {
        if (context.Project is not { } project || context.IsServer) { return; }

        project.FindEntityById(package.EntityId, entity => entity.FindComponent<ComponentFlu>()?.ApplyState(package));
    }
}
