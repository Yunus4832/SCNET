using MessagePack;

using MaxLengthAttribute = System.ComponentModel.DataAnnotations.MaxLengthAttribute;

namespace Game.Protocol.Terrain;

[MessagePackObject]
public readonly record struct EncodedTerrainChunkFragment(
    [property: Key(0)] ChunkAllocationId Allocation,
    [property: Key(1)] long ContentVersion,
    [property: Key(2)] int TotalLength,
    [property: Key(3)] ushort FragmentIndex,
    [property: Key(4)] ushort FragmentCount,
    [property: Key(5), MaxLength(ushort.MaxValue)]
    byte[] Payload
);
