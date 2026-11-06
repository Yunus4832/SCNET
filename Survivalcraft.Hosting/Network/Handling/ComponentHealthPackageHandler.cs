namespace Game.Network.Handling;

public class ComponentHealthPackageHandler : PackageHandlerBase<ComponentHealthPackage>
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
        if (context.Project is null || !AcceptsDirection(package.Type, context.IsServer))
        {
            return;
        }

        var project = context.Project;
        if (context.IsServer)
        {
            if (package is not EntityInjuryRequestPackage request) { return; }

            var player = project.FindSubsystem<SubsystemCharacters>(true)!.Characters
                .FirstOrDefault(data => ReferenceEquals(data.ControllerConnection, context.Sender))?.ComponentCharacter;
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

                        health.ApplyInjuryState(message2.Health, message2.Cause, message2.DeathTime, attacker);
                    }
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

                        h.ApplyHealthState(message4.Health, message4.Cause, message4.DeathTime);
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
                        h?.ApplyDamageState(message5.Health, message5.Destroyed, message5.IsAttack);
                    }
                );
                break;
            }
        }
    }
}
