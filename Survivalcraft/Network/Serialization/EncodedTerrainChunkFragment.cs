using Game.Terrains.Distribution;

namespace Game.Network.Serialization;

public readonly record struct EncodedTerrainChunkFragment(
    ChunkAllocationId Allocation,
    long ContentVersion,
    int TotalLength,
    ushort FragmentIndex,
    ushort FragmentCount,
    byte[] Payload);

public static class EncodedTerrainChunkFragmenter
{
    // 完整包还包含分片元数据、包标识和帧头，必须适配安全MTU的507字节可用预算。
    public const int DefaultFragmentPayloadSize = 448;

    public const int MaximumPayloadLength = 2 * 1024 * 1024;

    public const int MaximumFragmentCount =
        (MaximumPayloadLength + DefaultFragmentPayloadSize - 1) / DefaultFragmentPayloadSize;

    public static IEnumerable<EncodedTerrainChunkFragment> Split(
        EncodedTerrainChunk chunk,
        ChunkAllocationId allocation)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Payload.Length > MaximumPayloadLength)
        {
            throw new InvalidDataException($"Terrain chunk payload is too large: {chunk.Payload.Length}.");
        }

        var count = Math.Max(1, (chunk.Payload.Length + DefaultFragmentPayloadSize - 1) / DefaultFragmentPayloadSize);
        if (count > ushort.MaxValue)
        {
            throw new InvalidDataException($"Terrain chunk requires too many fragments: {count}.");
        }

        for (var index = 0; index < count; index++)
        {
            var offset = index * DefaultFragmentPayloadSize;
            var length = Math.Min(DefaultFragmentPayloadSize, chunk.Payload.Length - offset);
            yield return new EncodedTerrainChunkFragment(
                allocation,
                chunk.ContentVersion,
                chunk.Payload.Length,
                (ushort)index,
                (ushort)count,
                chunk.Payload.AsSpan(offset, length).ToArray());
        }
    }
}
