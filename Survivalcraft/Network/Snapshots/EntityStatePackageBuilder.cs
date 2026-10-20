namespace Game.Network.Snapshots;

public static class EntityStatePackageBuilder
{
    public static ComponentSicknessPackage Capture(ComponentSickness sickness) => new()
    { EntityId = sickness.Entity.EntityId, SicknessDuration = sickness.SicknessDuration };
    public static ComponentFluPackage Capture(ComponentFlu flu, ComponentFluPackage.EventType type) => new()
    {
        EntityId = flu.Entity.EntityId,
        PackageEventType = type,
        FluOnset = flu.FluOnset,
        FluDuration = flu.FluDuration,
        CoughDuration = flu.CoughDuration,
        SneezeDuration = flu.SneezeDuration
    };
    public static ComponentVitalStatPackage Capture(ComponentVitalStats stats) => new()
    {
        EntityId = stats.Entity.EntityId,
        PackageEventType = ComponentVitalStatPackage.EventType.SyncStat,
        Food = stats.Food,
        Sleep = stats.Sleep,
        Stamina = stats.Stamina,
        Wetness = stats.Wetness,
        Temperature = stats.Temperature
    };
    public static ComponentFurnacePackage Capture(IEnumerable<ComponentFurnace> furnaces) => new()
    {
        FurnaceDataList = furnaces.Select(f => new ComponentFurnacePackage.FurnacePackageData
        { EntityID = f.Entity.EntityId, FireTimeRemaining = f.FireTimeRemaining, SmeltingProgress = f.SmeltingProgress, HeatLevel = f.HeatLevel }).ToArray()
    };
    public static ComponentOnFirePackage Capture(ComponentOnFire target, ComponentCreature? attacker, float duration) => new()
    {
        Type = ComponentOnFirePackage.EventType.ComponentOnFire,
        EntityId = target.Entity.EntityId,
        AttackerEntityId = attacker?.Entity.EntityId ?? 0,
        Duration = duration
    };
    public static ComponentHealthPackage Capture(ComponentHealth health) => new()
    { Type = ComponentHealthPackage.EventType.SyncHealth, TargetId = health.Entity.EntityId, Health = health.Health };
    public static ComponentHealthPackage Capture(ComponentDamage damage) => new()
    { Type = ComponentHealthPackage.EventType.Damage, TargetId = damage.Entity.EntityId, Health = damage.HitPoints };
    public static ComponentHealthPackage Capture(ComponentHealth target, ComponentCreature? attacker, float amount,
        string cause, bool ignoreInvulnerability = false, bool isRequest = false,
        ComponentHealthPackage.RequestInjureType requestInjureType = ComponentHealthPackage.RequestInjureType.Unknown) => new()
        {
            Type = isRequest ? ComponentHealthPackage.EventType.RequestInjure : ComponentHealthPackage.EventType.Injure,
            TargetId = target.Entity.EntityId,
            AttackerId = attacker?.Entity.EntityId ?? 0,
            Amount = amount,
            Cause = cause,
            Health = target.Health,
            IgnoreInvulnerability = ignoreInvulnerability,
            InjureRequestType = requestInjureType
        };
}
