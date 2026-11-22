namespace Game.Terrains.Rendering;

/// <summary>由同一次光照采样操作生成的顶点范围。</summary>
internal readonly record struct TerrainVertexLightRun(int End, int Sample);
