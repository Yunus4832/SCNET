namespace Game.Terrains.Rendering;

/// <summary>分别计算地形分片几何数据与所采样光照的指纹。</summary>
internal readonly record struct TerrainSliceInputs(int Geometry, ulong Lighting);
