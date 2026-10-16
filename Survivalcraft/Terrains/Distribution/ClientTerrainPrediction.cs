namespace Game.Terrains.Distribution;

/// <summary>Temporary mesh overrides. Never writes authoritative cells or runs block behaviors.</summary>
public sealed class ClientTerrainPrediction(Terrain terrain, Action<Point2> invalidateGeometry)
{
    public const double TimeoutSeconds = 3;
    private const int _maximumPending = 64;

    private readonly Dictionary<int, PendingEdit> _pending = [];
    private int _nextId;

    public bool IsPending(Point3 cell) => _pending.Values.Any(pending =>
        pending.Cell == cell &&
        ReferenceEquals(terrain.GetChunkAtCell(cell.X, cell.Z, false), pending.Chunk));

    public int Begin(Point3 cell, int expectedValue, int replacementValue, double now)
    {
        if (!terrain.IsCellValid(cell.X, cell.Y, cell.Z))
        {
            return 0;
        }

        var chunk = terrain.GetChunkAtCell(cell.X, cell.Z, false);
        if (chunk is null || _pending.Count >= _maximumPending ||
            Terrain.ReplaceLight(terrain.GetCellValueFast(cell.X, cell.Y, cell.Z), 0) !=
            Terrain.ReplaceLight(expectedValue, 0))
        {
            return 0;
        }

        OnCellChanged(cell);
        _nextId = _nextId == int.MaxValue ? 1 : _nextId + 1;
        var index = TerrainChunk.CalculateCellIndex(cell.X & 15, cell.Y, cell.Z & 15);
        chunk.SetGeometryPrediction(index, expectedValue, replacementValue);
        _pending.Add(_nextId, new PendingEdit(cell, chunk, index, now + TimeoutSeconds));
        invalidateGeometry(chunk.Coords);
        return _nextId;
    }

    public void Complete(int requestId, Point3 cell, long contentVersion)
    {
        if (!_pending.TryGetValue(requestId, out var pending) || pending.Cell != cell)
        {
            return;
        }

        pending.RequiredVersion = contentVersion;
        if (pending.Chunk.NetworkContentVersion >= contentVersion)
        {
            Remove(requestId);
        }
    }

    public void OnCellChanged(Point3 cell)
    {
        foreach (var item in _pending.Where(item => item.Value.Cell == cell).ToArray())
        {
            Remove(item.Key);
        }
    }

    public void OnContentInstalled(TerrainChunk chunk)
    {
        foreach (var item in _pending.Where(item => ReferenceEquals(item.Value.Chunk, chunk)).ToArray())
        {
            Remove(item.Key);
        }
    }

    public void Update(double now)
    {
        foreach (var (id, pending) in _pending.ToArray())
        {
            if (now >= pending.ExpiresAt ||
                !ReferenceEquals(terrain.GetChunkAtCoords(pending.Chunk.Coords.X, pending.Chunk.Coords.Y), pending.Chunk) ||
                pending.RequiredVersion is { } version && pending.Chunk.NetworkContentVersion >= version)
            {
                Remove(id);
            }
        }
    }

    private void Remove(int id)
    {
        var pending = _pending[id];
        _pending.Remove(id);
        pending.Chunk.RemoveGeometryPrediction(pending.Index);
        if (ReferenceEquals(terrain.GetChunkAtCoords(pending.Chunk.Coords.X, pending.Chunk.Coords.Y), pending.Chunk))
        {
            invalidateGeometry(pending.Chunk.Coords);
        }
    }

    private sealed class PendingEdit(Point3 cell, TerrainChunk chunk, int index, double expiresAt)
    {
        public Point3 Cell { get; } = cell;
        public TerrainChunk Chunk { get; } = chunk;
        public int Index { get; } = index;
        public double ExpiresAt { get; } = expiresAt;
        public long? RequiredVersion { get; set; }
    }
}
