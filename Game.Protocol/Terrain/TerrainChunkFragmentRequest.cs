namespace Game.Protocol.Terrain;

public readonly record struct TerrainChunkFragmentRequest(
    ChunkAllocationId Allocation,
    long ContentVersion,
    ushort FragmentCount,
    ushort[] MissingFragmentIndices
);
