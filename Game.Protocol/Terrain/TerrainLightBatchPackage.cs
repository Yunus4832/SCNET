using MessagePack;

namespace Game.Protocol.Terrain;

/// <summary>Small related light updates with one presentation boundary.</summary>
[MessagePackObject]
public sealed record TerrainLightBatchPackage : IProtocolMessage
{
    // A level-15 source can reach four edge neighbors and two diagonal neighbors.
    public const int MaximumUpdates = 7;

    [Key(0)] public TerrainLightSectionsPackage[] Updates { get; init; } = [];
}
