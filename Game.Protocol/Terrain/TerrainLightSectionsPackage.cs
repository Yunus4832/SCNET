using MessagePack;

namespace Game.Protocol.Terrain;

/// <summary>一次内容分配对应的权威光照分段，可独立于方块载荷传输。</summary>
[MessagePackObject]
public sealed record TerrainLightSectionsPackage : IProtocolMessage
{
    [Key(0)] public ChunkAllocationId Allocation { get; init; }

    [Key(1)] public long ContentVersion { get; init; }

    [Key(2)] public long Sequence { get; init; }

    [Key(3)] public long BaseSequence { get; init; }

    [Key(4)] public bool IsBaseline { get; init; }

    [Key(5)] public ushort SectionMask { get; init; }

    // One packed uniform value or 4096 packed sky/block values. Uniform zero explicitly clears a section.
    [Key(6)] public byte[][] Sections { get; init; } = [];
}
