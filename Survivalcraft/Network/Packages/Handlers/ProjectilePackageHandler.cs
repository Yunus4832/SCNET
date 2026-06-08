namespace Game.Network.Packages.Handlers;

public sealed class ProjectilePackageHandler : PackageHandlerBase<ProjectilePackage>
{
    public override void Handle(ProjectilePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{nameof(ProjectilePackage)}");
            return;
        }

        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystem = project.FindSubsystem<SubsystemProjectiles>(true)!;
        ComponentCreature? creature = null;
        if (package.OwnerId != 0)
        {
            project.FindEntityById(
                package.OwnerId,
                entity => { creature = entity.FindComponent<ComponentCreature>(); }
            );
        }

        _ = package.IsFireProjectile
            ? subsystem.FireProjectileNet(
                package.Value,
                package.Position,
                package.Velocity,
                package.AngularVelocity,
                creature
            )
            : subsystem.AddProjectileNet(
                package.Value,
                package.Position,
                package.Velocity,
                package.AngularVelocity,
                creature
            );
        if (isServer)
        {
            netNode.QueuePackage(package);
        }
    }
}
