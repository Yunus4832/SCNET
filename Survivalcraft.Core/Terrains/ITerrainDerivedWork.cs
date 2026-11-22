namespace Game.Terrains;

/// <summary>可选的客户端派生任务；CPU 地形选择和依赖调度由外部管理。</summary>
public interface ITerrainDerivedWork
{
    bool HasCurrentGeometry(TerrainChunk chunk);

    bool HasUploadedGeometry(TerrainChunk chunk);

    bool HasPendingLightingPresentation(TerrainChunk chunk);

    bool CanUpdateBeforeLighting(TerrainChunk chunk);

    TerrainDerivedWorkResult UpdateChunk(TerrainChunk chunk);
}
