namespace Game.Network.Packages.Handlers;

public sealed class ComponentHealthPackageHandler : PackageHandlerBase<ComponentHealthPackage>
{
    public override void Handle(ComponentHealthPackage package, NetNode? netNode, bool isServer)
    {
        if (GameManager.Project is null)
        {
            return;
        }

        var project = GameManager.Project;
        switch (package.Type)
        {
            case ComponentHealthPackage.EventType.RequestInjure:
                project.FindEntityById(package.TargetId, entity =>
                {
                    var health = entity.FindComponent<ComponentHealth>();
                    if (health == null)
                    {
                        return;
                    }

                    if (package.AttackerId == 0)
                    {
                        health.Injure(package.Amount, null, package.IgnoreInvulnerability, package.Cause);
                    }
                    else
                    {
                        project.FindEntityById(package.AttackerId, entity2 =>
                        {
                            var attacker = entity2.FindComponent<ComponentCreature>();
                            health.Injure(package.Amount, attacker, package.IgnoreInvulnerability, package.Cause);
                        });
                    }
                });

                break;
            case ComponentHealthPackage.EventType.Injure:
                project.FindEntityById(package.TargetId, entity =>
                {
                    var health = entity.FindComponent<ComponentHealth>();
                    ComponentCreature? attacker;
                    if (health == null)
                    {
                        return;
                    }

                    if (package.AttackerId == 0)
                    {
                        health.NetInjure(package.Amount, null, package.Cause);
                        health.Health = package.Health;
                    }
                    else
                    {
                        project.FindEntityById(package.AttackerId, entity2 =>
                        {
                            attacker = entity2.FindComponent<ComponentCreature>();
                            health.NetInjure(package.Amount, attacker, package.Cause);
                            health.Health = package.Health;
                        });
                    }
                });
                break;
            case ComponentHealthPackage.EventType.HitResult:
                var particleSystem =
                    new HitValueParticleSystem(package.Position, package.Velocity, package.Color, package.Text);
                var pitch = new Random().Float(-0.2f, 0.2f);
                project.FindSubsystem<SubsystemParticles>(true)!.AddParticleSystem(particleSystem);
                project.FindSubsystem<SubsystemAudio>(true)!.PlaySound("Audio/Swoosh", 1f, pitch, package.Position, 3f,
                    false);
                break;
            case ComponentHealthPackage.EventType.SyncHealth:
                project.FindEntityById(package.TargetId, e =>
                {
                    var h = e.FindComponent<ComponentHealth>();
                    if (h == null)
                    {
                        return;
                    }

                    h.Health = package.Health;
                    // 服务端在死亡时通过 SyncHealth 携带死亡原因，
                    // 客户端在此应用，避免死亡视角显示“未知原因”。
                    if (h.Health == 0f && !string.IsNullOrEmpty(package.Cause))
                    {
                        h.CauseOfDeath = package.Cause;
                    }
                });
                break;
            case ComponentHealthPackage.EventType.Damage:
                project.FindEntityById(package.TargetId, e =>
                {
                    var h = e.FindComponent<ComponentDamage>();
                    h?.HitPoints = package.Health;
                });
                break;
        }
    }
}
