namespace Game.Terrains.Rendering;

/// <summary>A range of vertices emitted by the same light sampling operation.</summary>
internal readonly record struct TerrainVertexLightRun(int End, int Sample);
