namespace Game.Protocol.Terrain;

public readonly record struct EncodedTerrainChunkFragment(
    ChunkAllocationId Allocation,
    long ContentVersion,
    int TotalLength,
    ushort FragmentIndex,
    ushort FragmentCount,
    byte[] Payload
);
