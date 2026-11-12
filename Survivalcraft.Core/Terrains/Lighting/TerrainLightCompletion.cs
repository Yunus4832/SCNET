namespace Game.Terrains.Lighting;

/// <summary>Terminal private computation delivered to the world owner.</summary>
internal readonly record struct TerrainLightCompletion(
    TerrainLightTask Task,
    TerrainLightSnapshot? Snapshot,
    Exception? Error,
    TimeSpan Elapsed
);
