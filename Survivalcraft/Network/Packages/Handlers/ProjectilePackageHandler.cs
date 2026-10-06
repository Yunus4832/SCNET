namespace Game.Network.Packages.Handlers;

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
        if (package.Type == ProjectilePackage.EventType.Effect)
        {
            subsystem.PlayReplicatedEffect(package.Effect, package.Value, package.Position);
            return;
        }
        if (package.Type == ProjectilePackage.EventType.Update)
        {
            if (subsystem.FindProjectile(package.NetworkId) is { } projectile)
            {
                SubsystemProjectiles.ApplyNetworkMotion(projectile, package);
            }

            return;
        }
        if (package.Type == ProjectilePackage.EventType.Remove)
        {
            subsystem.RemoveProjectileNet(package.NetworkId);
            return;
        }

        ComponentCreature? creature = null;
        if (package.OwnerId != 0)
        {
            project.FindEntityById(
                package.OwnerId,
                entity => { creature = entity.FindComponent<ComponentCreature>(); }
            );
        }

        if (subsystem.FindProjectile(package.NetworkId) is not null)
        {
            return;
        }

        subsystem.AddReplicatedProjectile(package, creature);
    }
}
