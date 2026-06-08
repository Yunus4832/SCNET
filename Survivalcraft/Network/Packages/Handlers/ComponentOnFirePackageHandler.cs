namespace Game.Network.Packages.Handlers;

public sealed class ComponentOnFirePackageHandler : PackageHandlerBase<ComponentOnFirePackage>
{
    public override void Handle(ComponentOnFirePackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (package.Type)
        {
            case ComponentOnFirePackage.EventType.BlockOnFireAdd:
                project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.AddFireNet(
                    package.X,
                    package.Y,
                    package.Z,
                    package.Duration
                );
                break;
            case ComponentOnFirePackage.EventType.BlockOnFireRemove:
                project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.RemoveFireNet(
                    package.X,
                    package.Y,
                    package.Z
                );
                break;
            case ComponentOnFirePackage.EventType.ComponentOnFire:
                project.FindEntityById(package.EntityId, e =>
                {
                    var onFire = e.FindComponent<ComponentOnFire>();
                    if (onFire == null)
                    {
                        return;
                    }

                    if (package.AttackerEntityId == 0)
                    {
                        onFire.SetOnFireNet(null, package.Duration);
                    }
                    else
                    {
                        project.FindEntityById(package.AttackerEntityId, e2 =>
                        {
                            var creature = e2.FindComponent<ComponentCreature>();
                            onFire.SetOnFireNet(creature, package.Duration);
                        });
                    }
                });
                break;
        }
    }
}
