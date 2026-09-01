namespace Game.Terrains.Distribution;

/// <summary>
///     A snapshot addressed to one specific client allocation lifetime.
/// </summary>
public sealed class ClientChunkSnapshot
{
    public ClientChunkSnapshot(
        ChunkAllocationId allocation,
        long contentVersion,
        int[] cells,
        long[] shafts)
    {
        var authoritySnapshot = new AuthorityChunkSnapshot(
            allocation.Coords,
            contentVersion,
            cells,
            shafts);
        Allocation = allocation;
        ContentVersion = authoritySnapshot.ContentVersion;
        Cells = authoritySnapshot.Cells;
        Shafts = authoritySnapshot.Shafts;
    }

    public ChunkAllocationId Allocation { get; }

    public long ContentVersion { get; }

    public ReadOnlyMemory<int> Cells { get; }

    public ReadOnlyMemory<long> Shafts { get; }
}
