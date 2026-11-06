namespace Game.Network.Snapshots;

public static class EntityStatePackageBuilder
{
    public static ComponentSicknessPackage Capture(ComponentSickness sickness) => new()
    {
        EntityId = sickness.Entity.EntityId, SicknessDuration = sickness.SicknessDuration,
        EffectSequence = sickness.EffectSequence, PukeDuration = sickness.PukeDuration,
        NauseaDuration = sickness.NauseaDuration, IsNausea = sickness.IsNausea
    };

    public static ComponentFluPackage Capture(ComponentFlu flu, ComponentFluPackage.EventType type) => new()
    {
        EntityId = flu.Entity.EntityId,
        PackageEventType = type,
        FluOnset = flu.FluOnset,
        FluDuration = flu.FluDuration,
        CoughDuration = flu.CoughDuration,
        SneezeDuration = flu.SneezeDuration, EffectSequence = flu.EffectSequence,
        BlackoutDuration = flu.BlackoutDuration, LastWasCough = flu.LastWasCough
    };

    public static ComponentVitalStatPackage Capture(ComponentVitalStats stats) =>
        new VitalStatePackage
        {
            EntityId = stats.Entity.EntityId, Food = stats.Food, Sleep = stats.Sleep, Stamina = stats.Stamina,
            Wetness = stats.Wetness, Temperature = stats.Temperature,
            EnvironmentTemperature = stats.EnvironmentTemperature,
            SleepBlackoutDuration = stats.SleepBlackoutDuration, SleepBlackoutSequence = stats.SleepBlackoutSequence
        };

    public static ComponentFurnacePackage Capture(IEnumerable<ComponentFurnace> furnaces) => new()
    {
        FurnaceDataList = furnaces.Select(f => new ComponentFurnacePackage.FurnacePackageData
            {
                EntityID = f.Entity.EntityId, FireTimeRemaining = f.FireTimeRemaining,
                SmeltingProgress = f.SmeltingProgress, HeatLevel = f.HeatLevel
            }
        ).ToArray()
    };

    public static ComponentOnFirePackage Capture(ComponentOnFire target) =>
        new EntityFirePackage
        {
            EntityId = target.Entity.EntityId, AttackerEntityId = target.Attacker?.Entity.EntityId ?? 0,
            Duration = target.FireDuration
        };

    public static EntityHealthPackage Capture(ComponentHealth health) =>
        new()
        {
            TargetId = health.Entity.EntityId, Health = health.Health, Cause = health.CauseOfDeath,
            DeathTime = health.DeathTime
        };

    public static EntityDamagePackage Capture(ComponentDamage damage, bool destroyed = false, bool isAttack = false) =>
        new()
        {
            TargetId = damage.Entity.EntityId, Health = damage.HitPoints, Destroyed = destroyed, IsAttack = isAttack
        };

    public static ComponentHealthPackage Capture(
        ComponentHealth target,
        ComponentCreature? attacker,
        float amount,
        LocalizedText cause,
        bool ignoreInvulnerability = false,
        bool isRequest = false,
        ComponentHealthPackage.RequestInjureType requestInjureType = ComponentHealthPackage.RequestInjureType.Unknown
    ) => isRequest
        ? new EntityInjuryRequestPackage
        {
            TargetId = target.Entity.EntityId, AttackerId = attacker?.Entity.EntityId ?? 0, Amount = amount,
            Cause = cause, Health = target.Health, IgnoreInvulnerability = ignoreInvulnerability,
            InjureRequestType = requestInjureType
        }
        : new EntityInjuryPackage
        {
            TargetId = target.Entity.EntityId, AttackerId = attacker?.Entity.EntityId ?? 0, Amount = amount,
            Cause = target.Health == 0f ? target.CauseOfDeath : cause, Health = target.Health,
            IgnoreInvulnerability = ignoreInvulnerability, DeathTime = target.DeathTime
        };
}
