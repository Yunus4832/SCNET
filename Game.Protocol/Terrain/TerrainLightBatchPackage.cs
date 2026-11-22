using MessagePack;

namespace Game.Protocol.Terrain;

/// <summary>在同一次画面更新中生效的一组小范围关联光照变更。</summary>
[MessagePackObject]
public sealed record TerrainLightBatchPackage : IProtocolMessage
{
    // A level-15 source can reach four edge neighbors and two diagonal neighbors.
    public const int MaximumUpdates = 7;

    [Key(0)] public TerrainLightSectionsPackage[] Updates { get; init; } = [];
}
