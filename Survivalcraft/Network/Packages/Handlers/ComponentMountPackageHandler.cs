namespace Game.Network.Packages.Handlers;

public sealed class ComponentMountPackageHandler : PackageHandlerBase<ComponentMountPackage>
{
    internal static bool AcceptsDirection(ComponentMountPackage.EventType type, bool isServer)
    {
        return isServer
            ? type is ComponentMountPackage.EventType.MountRequest or ComponentMountPackage.EventType.DismountRequest
            : type is ComponentMountPackage.EventType.Mount or ComponentMountPackage.EventType.Dismount;
    }

    public override void Handle(ComponentMountPackage package, PackageReceiveContext context)
    {
        var isServer = context.IsServer;
        if (GameManager.Project is null || !AcceptsDirection(package.Type, isServer))
        {
            return;
        }

        var project = GameManager.Project;
        var player = project.FindSubsystem<SubsystemPlayers>(true)!.PlayersData
            .FirstOrDefault(playerData => ReferenceEquals(playerData.Client, context.Sender));
        if (isServer && (context.Sender is null ||
                         player?.ComponentPlayer?.Entity.EntityId != package.FromId))
        {
            return;
        }

        switch (package.Type)
        {
            case ComponentMountPackage.EventType.Dismount:
                project.FindEntityById(package.FromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    if (rider is null)
                    {
                        return;
                    }

                    // rider是骑乘者
                    rider.StartNetDismounting();
                    if (isServer || rider.Mount == null)
                    {
                        return;
                    }

                    // 禁用骑乘生物的组件行为
                    var select = rider.Mount.Entity.FindComponent<ComponentBehaviorSelector>();
                    select?.IsDisableBehavior = true;
                });
                break;
            case ComponentMountPackage.EventType.DismountRequest:
                project.FindEntityById(package.FromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    rider?.StartDismounting();
                });
                break;
            case ComponentMountPackage.EventType.Mount:
                project.FindEntityById(package.FromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    if (rider is null)
                    {
                        return;
                    }

                    project.FindEntityById(package.TargetId, entity2 =>
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

                        // 启动骑乘生物的组件行为
                        var select = mount.Entity.FindComponent<ComponentBehaviorSelector>();
                        select?.IsDisableBehavior = false;
                    });
                });
                break;
            case ComponentMountPackage.EventType.MountRequest:
                project.FindEntityById(package.FromId, entity =>
                {
                    var rider = entity.FindComponent<ComponentRider>();
                    project.FindEntityById(package.TargetId, entity2 =>
                    {
                        var mount = entity2.FindComponent<ComponentMount>();
                        if (mount != null && rider != null &&
                            project.FindSubsystem<SubsystemNetworkInterest>(true)!
                                .IsPositionRelevant(context.Sender!, mount.ComponentBody.Position.XZ) &&
                            rider.ScoreMount(mount, 2.5f) > 0f)
                        {
                            rider.StartMounting(mount);
                        }
                    });
                });
                break;
        }
    }
}
