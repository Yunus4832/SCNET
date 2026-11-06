namespace Game.Terrains.Distribution;

/// <summary>
///     A snapshot addressed to one specific client allocation lifetime.
/// </summary>
public sealed class ClientChunkSnapshot
{
    public static ClientChunkSnapshot Capture(ChunkAllocationId allocation, AuthorityChunkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (allocation.Coords != snapshot.Coords)
        {
            throw new ArgumentException("Snapshot coordinates must match the allocation.", nameof(snapshot));
        }

        var cells = snapshot.Cells.ToArray();
        var shafts = snapshot.Shafts.ToArray();
        for (var index = 0; index < cells.Length; index++) { cells[index] = Terrain.ReplaceLight(cells[index], 0); }

        for (var index = 0; index < shafts.Length; index++)
        {
            var shaft = Terrain.ReplaceTemperature(0, Terrain.ExtractTemperature(shafts[index]));
            shafts[index] = Terrain.ReplaceHumidity(shaft, Terrain.ExtractHumidity(shafts[index]));
        }

        return new ClientChunkSnapshot(allocation, snapshot.ContentVersion, cells, shafts);
    }

    public ClientChunkSnapshot(
        ChunkAllocationId allocation,
        long contentVersion,
        int[] cells,
        long[] shafts
    )
    {
        var authoritySnapshot = new AuthorityChunkSnapshot(
            allocation.Coords,
            contentVersion,
            cells,
            shafts
        );
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
