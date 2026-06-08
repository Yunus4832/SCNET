using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentMountPackage
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
            case EventType.Dismount:
                project.FindEntityById(FromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    if (rider is null)
                    {
                        return;
                    }

                    //rider是骑乘者
                    rider.StartNetDismounting();
                    if (isServer || rider.Mount == null)
                    {
                        return;
                    }

                    //禁用骑乘生物的组件行为
                    var select = rider.Mount.Entity.FindComponent<ComponentBehaviorSelector>();
                    select?.IsDisableBehavior = true;
                });
                break;
            case EventType.DismountRequest:
                project.FindEntityById(FromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    rider?.StartDismounting();
                });
                break;
            case EventType.Mount:
                project.FindEntityById(FromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    if (rider is null)
                    {
                        return;
                    }

                    project.FindEntityById(TargetId, entity2 =>
                    {
                        var mount = entity2.FindComponent<ComponentMount>();
                        if (mount == null)
                        {
                            return;
                        }

                        rider.StartNetMounting(mount);
                        if (isServer)
                        {
                            return;
                        }

                        //启动骑乘生物的组件行为
                        var select = mount.Entity.FindComponent<ComponentBehaviorSelector>();
                        select?.IsDisableBehavior = false;
                    });
                });
                break;
            case EventType.MountRequest:
                project.FindEntityById(FromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    project.FindEntityById(TargetId, entity2 =>
                    {
                        var mount = entity2.FindComponent<ComponentMount>();
                        if (mount != null && rider != null)
                        {
                            rider.StartMounting(mount);
                        }
                    });
                });
                break;
        }
    }
}

public sealed class ComponentMountPackageHandler : PackageHandlerBase<ComponentMountPackage>
{
    public override void Handle(ComponentMountPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentMountPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
