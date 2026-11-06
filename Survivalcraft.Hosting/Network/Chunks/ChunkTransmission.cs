namespace Game.Network.Chunks;

/// <summary>A bounded, resumable fragment plan; repeated requests merge without restarting the stream.</summary>
public sealed class ChunkTransmission
{
    private readonly SortedSet<ushort> _remaining = [];

    private readonly HashSet<ushort> _sent = [];

    private readonly HashSet<ushort> _retries = [];

    public EncodedTerrainChunk Encoded { get; }

    public ChunkAllocationId Allocation { get; }

    public ChunkTransmission(EncodedTerrainChunk encoded, ChunkAllocationId allocation)
    {
        ArgumentNullException.ThrowIfNull(encoded);
        if (encoded.Payload is null ||
            encoded.Payload.Length > EncodedTerrainChunkFragmenter.MaximumPayloadLength ||
            allocation.Generation == 0 ||
            encoded.Coords != allocation.Coords ||
            encoded.ContentVersion < 0)
        {
            throw new InvalidDataException("Invalid chunk transmission metadata.");
        }

        Encoded = encoded;
        Allocation = allocation;
    }

    public bool Started { get; private set; }

    public bool IsComplete => _remaining.Count == 0;

    public int FragmentCount => Math.Max(
        1,
        (Encoded.Payload.Length + EncodedTerrainChunkFragmenter.DefaultFragmentPayloadSize - 1) /
        EncodedTerrainChunkFragmenter.DefaultFragmentPayloadSize
    );

    public void RequestAll()
    {
        for (var index = 0; index < FragmentCount; index++)
        {
            var value = (ushort)index;
            _remaining.Add(value);
            if (_sent.Contains(value)) { _retries.Add(value); }
        }
    }

    public bool RequestMissing(TerrainChunkFragmentRequest request, bool repairOnly = false)
    {
        if (request.Allocation != Allocation ||
            request.ContentVersion != Encoded.ContentVersion ||
            request.FragmentCount != FragmentCount ||
            request.MissingFragmentIndices.Length == 0 ||
            request.MissingFragmentIndices.Any(index => index >= FragmentCount)) { return false; }

        foreach (var index in request.MissingFragmentIndices)
        {
            _remaining.Add(index);
            if (repairOnly || _sent.Contains(index)) { _retries.Add(index); }
        }

        return true;
    }

    public bool TrySend(
        ChunkSendBudget budget,
        Action<EncodedTerrainChunkFragment> send,
        out bool retransmitted,
        out int bytes
    )
    {
        retransmitted = false;
        bytes = 0;
        if (IsComplete) { return false; }

        var index = _remaining.Min;
        var offset = index * EncodedTerrainChunkFragmenter.DefaultFragmentPayloadSize;
        var length = Math.Min(
            EncodedTerrainChunkFragmenter.DefaultFragmentPayloadSize,
            Encoded.Payload.Length - offset
        );
        if (!budget.TrySpend(length, !Started)) { return false; }

        send(
            new EncodedTerrainChunkFragment(
                Allocation,
                Encoded.ContentVersion,
                Encoded.Payload.Length,
                index,
                (ushort)FragmentCount,
                Encoded.Payload.AsSpan(offset, length).ToArray()
            )
        );
        Started = true;
        _remaining.Remove(index);
        _sent.Add(index);
        retransmitted = _retries.Remove(index);
        bytes = length;
        return true;
    }
}
