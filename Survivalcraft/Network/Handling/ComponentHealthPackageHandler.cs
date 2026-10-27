using Game.Network.Transport;

namespace Game.Network.Handling;

public sealed class ComponentHealthPackageHandler : PackageHandlerBase<ComponentHealthPackage>
{
    internal static bool AcceptsDirection(ComponentHealthPackage.EventType type, bool isServer)
    {
        return isServer
            ? type == ComponentHealthPackage.EventType.RequestInjure
            : type is ComponentHealthPackage.EventType.Injure or ComponentHealthPackage.EventType.HitResult or
                ComponentHealthPackage.EventType.SyncHealth or ComponentHealthPackage.EventType.Damage;
    }

    public override void Handle(ComponentHealthPackage package, PackageReceiveContext context)
    {
        if (GameManager.Project is null || !AcceptsDirection(package.Type, context.IsServer))
        {
            return;
        }

        var project = GameManager.Project;
        if (context.IsServer)
        {
            if (package is not EntityInjuryRequestPackage request) { return; }

            var player = project.FindSubsystem<SubsystemCharacters>(true)!.Characters
                .FirstOrDefault(data => ReferenceEquals(data.Client, context.Sender))?.ComponentCharacter;
            var mount = player?.Entity.FindComponent<ComponentRider>()?.Mount;
            if (context.Sender is null ||
                player is null ||
                (request.TargetId != player.Entity.EntityId && request.TargetId != mount?.Entity.EntityId) ||
                request.AttackerId != 0 ||
                (request.IgnoreInvulnerability &&
                 (request.InjureRequestType != ComponentHealthPackage.RequestInjureType.Choke ||
                  request.TargetId != player.Entity.EntityId ||
                  request.Amount != 0.1f)) ||
                !float.IsFinite(request.Amount) ||
                request.Amount <= 0f)
            {
                return;
            }
        }

        switch (package)
        {
            case EntityInjuryRequestPackage message1:
            {
                project.FindEntityById(
                    message1.TargetId,
                    entity =>
                    {
                        var health = entity.FindComponent<ComponentHealth>();
                        if (health == null)
                        {
                            return;
                        }

                        if (message1.AttackerId == 0)
                        {
                            health.Injure(message1.Amount, null, message1.IgnoreInvulnerability, message1.Cause);
                        }
                        else
                        {
                            project.FindEntityById(
                                message1.AttackerId,
                                entity2 =>
                                {
                                    var attacker = entity2.FindComponent<ComponentCreature>();
                                    health.Injure(
                                        message1.Amount,
                                        attacker,
                                        message1.IgnoreInvulnerability,
                                        message1.Cause
                                    );
                                }
                            );
                        }
                    }
                );

                break;
            }
            case EntityInjuryPackage message2:
            {
                project.FindEntityById(
                    message2.TargetId,
                    entity =>
                    {
                        var health = entity.FindComponent<ComponentHealth>();
                        ComponentCreature? attacker = null;
                        if (health == null)
                        {
                            return;
                        }

                        if (message2.AttackerId != 0)
                        {
                            project.FindEntityById(
                                message2.AttackerId,
                                entity2 =>
                                {
                                    attacker = entity2.FindComponent<ComponentCreature>();
                                }
                            );
                        }

                        health.NetInjure(message2.Amount, attacker, message2.Cause);
                        health.Health = message2.Health;
                    }
                );
                break;
            }
            case CharacterHitEffectPackage message3:
            {
                var particleSystem =
                    new HitValueParticleSystem(message3.Position, message3.Velocity, message3.Color, message3.Text);
                var pitch = new Random().Float(-0.2f, 0.2f);
                project.FindSubsystem<SubsystemParticles>(true)!.AddParticleSystem(particleSystem);
                project.FindSubsystem<SubsystemAudio>(true)!.PlaySound(
                    "Audio/Swoosh",
                    1f,
                    pitch,
                    message3.Position,
                    3f,
                    false
                );
                break;
            }
            case EntityHealthPackage message4:
            {
                project.FindEntityById(
                    message4.TargetId,
                    e =>
                    {
                        var h = e.FindComponent<ComponentHealth>();
                        if (h == null)
                        {
                            return;
                        }

                        h.Health = message4.Health;
                        // 服务端在死亡时通过 SyncHealth 携带死亡原因，
                        // 客户端在此应用，避免死亡视角显示“未知原因”。
                        if (h.Health == 0f && !string.IsNullOrEmpty(message4.Cause))
                        {
                            h.CauseOfDeath = message4.Cause;
                        }
                    }
                );
                break;
            }
            case EntityDamagePackage message5:
            {
                project.FindEntityById(
                    message5.TargetId,
                    e =>
                    {
                        var h = e.FindComponent<ComponentDamage>();
                        h?.HitPoints = message5.Health;
                    }
                );
                break;
            }
        }
    }
}
