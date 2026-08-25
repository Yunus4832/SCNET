using Game.Terrains.Distribution;

namespace Game.Network.Serialization;

public readonly record struct TerrainChunkFragmentRequest(
    ChunkAllocationId Allocation,
    long ContentVersion,
    ushort FragmentCount,
    ushort[] MissingFragmentIndices);
