namespace Game.Terrains.Distribution;

public interface IChunkContentTransport
{
    void Request(IReadOnlyList<ChunkContentRequest> requests);

    void Discard(ChunkAllocationId allocation);

    int DrainReceived(ICollection<ClientChunkSnapshot> destination, int maximumCount = int.MaxValue);

    int DrainDeltas(ICollection<TerrainCellDelta> destination, int maximumCount = 1024);

    int DrainResyncRequests(ICollection<ChunkContentResync> destination, int maximumCount = int.MaxValue);
}
