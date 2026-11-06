namespace Game.Terrains.Distribution;

/// <summary>
///     Immutable-by-contract view of authoritative chunk contents.
/// </summary>
public sealed class AuthorityChunkSnapshot
{
    public const int CellCount = 16 * 16 * 256;

    public const int ShaftCount = 16 * 16;

    public AuthorityChunkSnapshot(Point2 coords, long contentVersion, int[] cells, long[] shafts)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(shafts);
        if (contentVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(contentVersion));
        }

        if (cells.Length != CellCount)
        {
            throw new ArgumentException($"Chunk contents require exactly {CellCount} cells.", nameof(cells));
        }

        if (shafts.Length != ShaftCount)
        {
            throw new ArgumentException($"Chunk contents require exactly {ShaftCount} shafts.", nameof(shafts));
        }

        Coords = coords;
        ContentVersion = contentVersion;
        Cells = cells;
        Shafts = shafts;
    }

    public Point2 Coords { get; }

    public long ContentVersion { get; }

    public ReadOnlyMemory<int> Cells { get; }

    public ReadOnlyMemory<long> Shafts { get; }
}
