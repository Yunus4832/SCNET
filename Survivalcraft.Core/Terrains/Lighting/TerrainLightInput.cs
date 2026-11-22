namespace Game.Terrains.Lighting;

/// <summary>记录空间光照计算所用的内容版本及区块分配标识。</summary>
public readonly record struct TerrainLightInput(
    ulong Allocation,
    long ContentRevision,
    long ContentVersion,
    long HeightRevision,
    TerrainDerivedRevisions.Request Request
);
