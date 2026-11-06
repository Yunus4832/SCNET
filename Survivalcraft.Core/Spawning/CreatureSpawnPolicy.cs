namespace Game.Spawning;

/// <summary>Immutable effective natural-spawn settings for one world lifetime.</summary>
public sealed record CreatureSpawnPolicy(
    int TotalLimit,
    int AreaLimit,
    int MaxPlayerAreaLimit,
    int MaxPointLimit,
    int AreaRadius,
    int TotalLimitConstant,
    int AreaLimitConstant,
    int AreaRadiusConstant,
    float SpawnIntervalTime,
    float ConstantSpawnIntervalTime
);
