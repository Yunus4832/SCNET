namespace Game.Terrains.Lighting;

/// <summary>提交给世界管理者的后台计算终态结果。</summary>
internal readonly record struct TerrainLightCompletion(
    TerrainLightTask Task,
    TerrainLightSnapshot? Snapshot,
    TerrainLightSnapshot[]? RegionSnapshots,
    Exception? Error,
    TimeSpan Elapsed
);
