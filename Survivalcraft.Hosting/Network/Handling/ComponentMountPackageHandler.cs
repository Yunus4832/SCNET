namespace Game.Network.Handling;

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
        if (context.Project is null || !AcceptsDirection(package.Type, isServer))
        {
            return;
        }

        var project = context.Project;
        var player = project.FindSubsystem<SubsystemCharacters>(true)!.Characters
            .FirstOrDefault(characterData => ReferenceEquals(characterData.ControllerConnection, context.Sender));
        if (isServer &&
            (context.Sender is null ||
             player?.ComponentCharacter?.Entity.EntityId != package.FromId))
        {
            return;
        }

        switch (package)
        {
            case DismountedPackage message1:
            {
                project.FindEntityById(
                    message1.FromId,
                    entity =>
                    {
                        var rider = entity.FindComponent<ComponentRider>();
                        if (rider is null)
                        {
                            return;
                        }

                        // rider是骑乘者
                        rider.StartNetDismounting();
                    }
                );
                break;
            }
            case DismountRequestPackage message2:
            {
                project.FindEntityById(
                    message2.FromId,
                    entity =>
                    {
                        var rider = entity.FindComponent<ComponentRider>();
                        rider?.StartDismounting();
                    }
                );
                break;
            }
            case MountedPackage message3:
            {
                project.FindEntityById(
                    message3.FromId,
                    entity =>
                    {
                        var rider = entity.FindComponent<ComponentRider>();
                        if (rider is null)
                        {
                            return;
                        }

                        project.FindEntityById(
                            message3.TargetId,
                            entity2 =>
                            {
                                var mount = entity2.FindComponent<ComponentMount>();
                                if (mount == null)
                                {
                                    return;
                                }

                                rider.StartNetMounting(mount);
                            }
                        );
                    }
                );
                break;
            }
            case MountRequestPackage message4:
            {
                project.FindEntityById(
                    message4.FromId,
                    entity =>
                    {
                        var rider = entity.FindComponent<ComponentRider>();
                        project.FindEntityById(
                            message4.TargetId,
                            entity2 =>
                            {
                                var mount = entity2.FindComponent<ComponentMount>();
                                if (mount != null &&
                                    rider != null &&
                                    project.FindSubsystem<SubsystemNetworkInterest>(true)!
                                        .IsPositionRelevant(context.Sender!, mount.ComponentBody.Position.XZ) &&
                                    rider.ScoreMount(mount, 2.5f) > 0f)
                                {
                                    rider.StartMounting(mount);
                                }
                            }
                        );
                    }
                );
                break;
            }
        }
    }
}
