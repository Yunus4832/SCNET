using Game.Network.Chunks;

namespace Game.Network;

/// <summary>Immutable delivery policy captured by the owner for one node lifetime.</summary>
public sealed class NetNodeOptions
{
    public static NetNodeOptions Default { get; } = new(
        NetworkTerrainPolicy.DefaultServerChunkCountSendPer,
        NetworkTerrainPolicy.DefaultServerChunkCountSendPer,
        NetworkTerrainPolicy.DefaultServerChunkBytesSendPerSecond
    );

    public int MaximumChunkEncodes { get; }

    public int ChunksPerSecond { get; }

    public int BytesPerSecond { get; }

    public NetNodeOptions(int maximumChunkEncodes, int chunksPerSecond, int bytesPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumChunkEncodes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunksPerSecond);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerSecond);
        MaximumChunkEncodes = maximumChunkEncodes;
        ChunksPerSecond = chunksPerSecond;
        BytesPerSecond = bytesPerSecond;
    }
}
