namespace Game.Spawning;

/// <summary>本次世界生命周期内生效的不可变自然生成设置。</summary>
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
