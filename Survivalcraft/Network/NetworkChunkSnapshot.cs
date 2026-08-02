using System.Buffers;

namespace Game.Network;

internal sealed class NetworkChunkSnapshot : IDisposable
{
    private const int _cellCount = 16 * 16 * 256;

    private const int _shaftCount = 16 * 16;

    private int[]? _cells;

    private long[]? _shafts;

    private NetworkChunkSnapshot(TerrainChunk source, long revision, int[] cells, long[] shafts)
    {
        Source = source;
        Revision = revision;
        Coords = source.Coords;
        _cells = cells;
        _shafts = shafts;
    }

    public int[] Cells => _cells ?? throw new ObjectDisposedException(nameof(NetworkChunkSnapshot));

    public Point2 Coords { get; }

    public long Revision { get; }

    public long[] Shafts => _shafts ?? throw new ObjectDisposedException(nameof(NetworkChunkSnapshot));

    public TerrainChunk Source { get; }

    public static NetworkChunkSnapshot? TryCapture(TerrainChunk chunk, int maximumAttempts = 2)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (maximumAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        }

        var cells = ArrayPool<int>.Shared.Rent(_cellCount);
        var shafts = ArrayPool<long>.Shared.Rent(_shaftCount);
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var revision = chunk.NetworkContentRevision;
            Array.Copy(chunk.Cells, cells, _cellCount);
            Array.Copy(chunk.Shafts, shafts, _shaftCount);
            if (revision == chunk.NetworkContentRevision)
            {
                return new NetworkChunkSnapshot(chunk, revision, cells, shafts);
            }
        }

        ArrayPool<int>.Shared.Return(cells);
        ArrayPool<long>.Shared.Return(shafts);
        return null;
    }

    public void Dispose()
    {
        var cells = Interlocked.Exchange(ref _cells, null);
        if (cells != null)
        {
            ArrayPool<int>.Shared.Return(cells);
        }

        var shafts = Interlocked.Exchange(ref _shafts, null);
        if (shafts != null)
        {
            ArrayPool<long>.Shared.Return(shafts);
        }
    }
}
