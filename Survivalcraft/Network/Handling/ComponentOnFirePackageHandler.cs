using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ComponentOnFirePackageHandler : PackageHandlerBase<ComponentOnFirePackage>
{
    public override void Handle(ComponentOnFirePackage package, PackageReceiveContext context)
    {
        if (GameManager.Project is null || context.IsServer)
        {
            return;
        }

        var project = GameManager.Project;
        switch (package)
        {
            case BlockFireAddedPackage message1:
            {
                project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.AddFireNet(
                    message1.X,
                    message1.Y,
                    message1.Z,
                    message1.Duration
                );
                break;
            }
            case BlockFireRemovedPackage message2:
            {
                project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.RemoveFireNet(
                    message2.X,
                    message2.Y,
                    message2.Z
                );
                break;
            }
            case EntityFirePackage message3:
            {
                project.FindEntityById(
                    message3.EntityId,
                    e =>
                    {
                        var onFire = e.FindComponent<ComponentOnFire>();
                        if (onFire == null)
                        {
                            return;
                        }

                        ComponentCreature? attacker = null;
                        if (message3.AttackerEntityId != 0)
                        {
                            project.FindEntityById(
                                message3.AttackerEntityId,
                                e2 =>
                                {
                                    attacker = e2.FindComponent<ComponentCreature>();
                                }
                            );
                        }

                        // 攻击者可能不在本客户端兴趣范围，不能因此丢失被观察实体的着火状态。
                        onFire.SetOnFireNet(attacker, message3.Duration);
                    }
                );
                break;
            }
        }
    }
}
