using Engine.Core.Mathematics;

using MessagePack;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public record struct TerrainUpdateLocation
{
    /// <summary>
    ///     Location 位置中心
    /// </summary>
    [Key(0)] public Vector2 Center;

    /// <summary>
    ///     上次更新的位置中心（用于检测位置变化）
    /// </summary>
    [Key(1)] public Vector2? LastChunksUpdateCenter;

    /// <summary>
    ///     可视距离
    /// </summary>
    [Key(2)] public float VisibilityDistance;

    /// <summary>
    ///     内容加载距离（一般大于等于可视距离）
    /// </summary>
    [Key(3)] public float ContentDistance;
}
