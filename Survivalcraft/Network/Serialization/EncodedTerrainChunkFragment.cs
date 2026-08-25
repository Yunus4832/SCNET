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
    public const int DefaultFragmentPayloadSize = 900;

    public const int MaximumPayloadLength = 2 * 1024 * 1024;

    public const int MaximumFragmentCount = 2331;

    public static IEnumerable<EncodedTerrainChunkFragment> Split(
        EncodedTerrainChunk chunk,
        ChunkAllocationId allocation,
        int fragmentPayloadSize = DefaultFragmentPayloadSize)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (chunk.Payload.Length > MaximumPayloadLength)
        {
            throw new InvalidDataException($"Terrain chunk payload is too large: {chunk.Payload.Length}.");
        }
        if (fragmentPayloadSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fragmentPayloadSize));
        }

        var count = Math.Max(1, (chunk.Payload.Length + fragmentPayloadSize - 1) / fragmentPayloadSize);
        if (count > ushort.MaxValue)
        {
            throw new InvalidDataException($"Terrain chunk requires too many fragments: {count}.");
        }

        for (var index = 0; index < count; index++)
        {
            var offset = index * fragmentPayloadSize;
            var length = Math.Min(fragmentPayloadSize, chunk.Payload.Length - offset);
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
