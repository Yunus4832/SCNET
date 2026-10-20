using Engine.Core;

namespace Game.Protocol.Terrain;

public record struct TerrainUpdateLocation
{

    /// <summary>
    ///     Location 位置中心
    /// </summary>
    public Vector2 Center;

    /// <summary>
    ///     上次更新的位置中心（用于检测位置变化）
    /// </summary>
    public Vector2? LastChunksUpdateCenter;

    /// <summary>
    ///     可视距离
    /// </summary>
    public float VisibilityDistance;

    /// <summary>
    ///     内容加载距离（一般大于等于可视距离）
    /// </summary>
    public float ContentDistance;
}
