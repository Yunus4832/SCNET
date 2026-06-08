using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentOnFirePackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (Type)
        {
            case EventType.BlockOnFireAdd:
                project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.AddFireNet(X, Y, Z, Duration);
                break;
            case EventType.BlockOnFireRemove:
                project.FindSubsystem<SubsystemFireBlockBehavior>(true)!.RemoveFireNet(X, Y, Z);
                break;
            case EventType.ComponentOnFire:
                project.FindEntityById(EntityId, e =>
                {
                    var onFire = e.FindComponent<ComponentOnFire>();
                    if (onFire == null)
                    {
                        return;
                    }

                    if (AttackerEntityId == 0)
                    {
                        onFire.SetOnFireNet(null, Duration);
                    }
                    else
                    {
                        project.FindEntityById(AttackerEntityId, e2 =>
                        {
                            var creature = e2.FindComponent<ComponentCreature>();
                            onFire.SetOnFireNet(creature, Duration);
                        });
                    }
                });
                break;
        }
    }
}

public sealed class ComponentOnFirePackageHandler : PackageHandlerBase<ComponentOnFirePackage>
{
    public override void Handle(ComponentOnFirePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentOnFirePackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
