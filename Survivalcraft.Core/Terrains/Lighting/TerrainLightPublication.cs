namespace Game.Terrains.Lighting;

/// <summary>由所属世界的依赖调度器消费的发布结果。</summary>
public readonly record struct TerrainLightPublication(bool Published, bool ValuesChanged, int ChangedBoundaryFaces);
