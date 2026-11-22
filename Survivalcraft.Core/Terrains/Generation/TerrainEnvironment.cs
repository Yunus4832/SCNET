namespace Game.Terrains.Generation;

/// <summary>为支持的地形生成配置创建环境查询对象。</summary>
public static class TerrainEnvironment
{
    public static ITerrainEnvironment Create(WorldSettings settings, int seed)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Generator.Definition.CreateEnvironment(settings, seed);
    }
}
