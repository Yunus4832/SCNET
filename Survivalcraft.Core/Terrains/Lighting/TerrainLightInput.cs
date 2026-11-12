namespace Game.Terrains.Lighting;

/// <summary>Content and allocation identity consumed by a spatial light computation.</summary>
public readonly record struct TerrainLightInput(
    ulong Allocation,
    long ContentRevision,
    long ContentVersion,
    long HeightRevision,
    TerrainDerivedRevisions.Request Request
);
