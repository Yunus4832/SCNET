namespace Game.Network.Packages.Handlers;

public sealed class ComponentOnFirePackageHandler : PackageHandlerBase<ComponentOnFirePackage>
{
    public override void Handle(ComponentOnFirePackage package, PackageReceiveContext context)
    {
        if (GameManager.Project is null || context.IsServer)
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

                    ComponentCreature? attacker = null;
                    if (package.AttackerEntityId != 0)
                    {
                        project.FindEntityById(package.AttackerEntityId, e2 =>
                        {
                            attacker = e2.FindComponent<ComponentCreature>();
                        });
                    }

                    // 攻击者可能不在本客户端兴趣范围，不能因此丢失被观察实体的着火状态。
                    onFire.SetOnFireNet(attacker, package.Duration);
                });
                break;
        }
    }
}
