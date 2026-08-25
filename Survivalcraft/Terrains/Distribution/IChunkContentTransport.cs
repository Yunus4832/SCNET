namespace Game.Terrains.Distribution;

public interface IChunkContentTransport
{
    void Request(IReadOnlyList<ChunkContentRequest> requests);

    int DrainReceived(ICollection<ClientChunkSnapshot> destination);

    int DrainDeltas(ICollection<TerrainCellDelta> destination);

    int DrainFailed(ICollection<ChunkAllocationId> destination);
}
