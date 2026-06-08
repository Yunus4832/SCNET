using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game.Network.Packages;

public partial class ComponentHealthPackage
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
            case EventType.RequestInjure:
                project.FindEntityById(TargetId, entity =>
                {
                    var health = entity.FindComponent<ComponentHealth>();
                    if (health == null)
                    {
                        return;
                    }

                    if (AttackerId == 0)
                    {
                        health.Injure(Amount, null, IgnoreInvulnerability, Cause);
                    }
                    else
                    {
                        project.FindEntityById(AttackerId, entity2 =>
                        {
                            var attacker = entity2.FindComponent<ComponentCreature>();
                            health.Injure(Amount, attacker, IgnoreInvulnerability, Cause);
                        });
                    }
                });

                break;
            case EventType.Injure:
                project.FindEntityById(TargetId, entity =>
                {
                    var health = entity.FindComponent<ComponentHealth>();
                    ComponentCreature? attacker;
                    if (health == null)
                    {
                        return;
                    }

                    if (AttackerId == 0)
                    {
                        health.NetInjure(Amount, null, Cause);
                        health.Health = Health;
                    }
                    else
                    {
                        project.FindEntityById(AttackerId, entity2 =>
                        {
                            attacker = entity2.FindComponent<ComponentCreature>();
                            health.NetInjure(Amount, attacker, Cause);
                            health.Health = Health;
                        });
                    }
                });
                break;
            case EventType.HitResult:
                var particleSystem = new HitValueParticleSystem(Position, Velocity, Color, Text);
                var pitch = new Random().Float(-0.2f, 0.2f);
                project.FindSubsystem<SubsystemParticles>(true)!.AddParticleSystem(particleSystem);
                project.FindSubsystem<SubsystemAudio>(true)!.PlaySound("Audio/Swoosh", 1f, pitch, Position, 3f, false);
                break;
            case EventType.SyncHealth:
                project.FindEntityById(TargetId, e =>
                {
                    var h = e.FindComponent<ComponentHealth>();
                    h?.Health = Health;
                });
                break;
            case EventType.Damage:
                project.FindEntityById(TargetId, e =>
                {
                    var h = e.FindComponent<ComponentDamage>();
                    h?.HitPoints = Health;
                });
                break;
        }
    }
}

public sealed class ComponentHealthPackageHandler : PackageHandlerBase<ComponentHealthPackage>
{
    public override void Handle(ComponentHealthPackage package, NetNode? netNode, bool isServer)
    {
        if (netNode == null)
        {
            Log.Information($"Package处理器需要NetNode:{typeof(ComponentHealthPackage).Name}");
            return;
        }

        package.HandleCore(netNode, isServer);
    }
}
