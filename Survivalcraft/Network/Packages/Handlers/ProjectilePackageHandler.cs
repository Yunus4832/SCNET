using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ProjectilePackage
{
    internal void HandleCore(NetNode netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        var subsystem = project.FindSubsystem<SubsystemProjectiles>(true)!;
        ComponentCreature? creature = null;
        if (OwnerId != 0)
        {
            project.FindEntityById(OwnerId, entity => { creature = entity.FindComponent<ComponentCreature>(); });
        }

        _ = IsFireProjectile
            ? subsystem.FireProjectileNet(Value, Position, Velocity, AngularVelocity, creature)
            : subsystem.AddProjectileNet(Value, Position, Velocity, AngularVelocity, creature);
        if (isServer)
        {
            netNode.QueuePackage(this);
        }
    }
}

public sealed class ProjectilePackageHandler : PackageHandlerBase<ProjectilePackage>
{
    public override void Handle(ProjectilePackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ProjectilePackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
