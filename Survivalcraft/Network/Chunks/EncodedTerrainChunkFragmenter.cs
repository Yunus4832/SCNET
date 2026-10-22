namespace Game.Network.Chunks;

public static class EncodedTerrainChunkFragmenter
{
    // 完整包还包含分片元数据、包标识和帧头，必须适配 Engine 预留中继头部后的安全 MTU（419 字节，含 DTLS 与中继预留）。
    public const int DefaultFragmentPayloadSize = ProtocolLimits.TerrainFragmentPayloadSize;

    public const int MaximumPayloadLength = ProtocolLimits.MaximumTerrainPayloadLength;

    public const int MaximumFragmentCount =
        ProtocolLimits.MaximumTerrainFragmentCount;

    public static IEnumerable<EncodedTerrainChunkFragment> Split(
        EncodedTerrainChunk chunk,
        ChunkAllocationId allocation
    )
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
                chunk.Payload.AsSpan(offset, length).ToArray()
            );
        }
    }
}
