using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ProjectilePackageHandler : PackageHandlerBase<ProjectilePackage>
{
    public override void Handle(ProjectilePackage package, PackageReceiveContext context)
    {
        var netNode = context.Node;
        var isServer = context.IsServer;
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ProjectilePackage)}");
            return;
        }

        if (isServer || GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystem = project.FindSubsystem<SubsystemProjectiles>(true)!;
        if (package is ProjectileEffectPackage message1)
        {
            subsystem.PlayReplicatedEffect(message1.Effect, message1.Value, message1.Position);
            return;
        }

        if (package is ProjectileStatePackage message2)
        {
            if (subsystem.FindProjectile(message2.NetworkId) is { } projectile)
            {
                SubsystemProjectiles.ApplyNetworkMotion(projectile, message2);
            }

            return;
        }

        if (package is ProjectileRemovedPackage message3)
        {
            subsystem.RemoveProjectileNet(message3.NetworkId);
            return;
        }

        if (package is not ProjectileAddedPackage added) { return; }

        ComponentCreature? creature = null;
        if (added.OwnerId != 0)
        {
            project.FindEntityById(
                added.OwnerId,
                entity => { creature = entity.FindComponent<ComponentCreature>(); }
            );
        }

        if (subsystem.FindProjectile(added.NetworkId) is not null)
        {
            return;
        }

        subsystem.AddReplicatedProjectile(added, creature);
    }
}
