using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public readonly record struct TerrainChunkFragmentRequest(
    [property: Key(0)] ChunkAllocationId Allocation,
    [property: Key(1)] long ContentVersion,
    [property: Key(2)] ushort FragmentCount,
    [property: Key(3), MaxLength(ProtocolLimits.MaximumTerrainFragmentCount)]
    ushort[] MissingFragmentIndices
);
