using Game.Terrains.Lighting;

namespace Game.Terrains;

public class TerrainChunk : IDisposable
{
    private const int _size = 16;

    private const int _height = 256;

    public const int SlicesCount = _height / SliceHeight;

    private const int _heightMinusOne = 255;

    public const int SizeMinusOne = 15;

    public const int SizeBits = 4;

    public const int HeightBits = 8;

    public const int SliceHeight = 16;

    public bool AreBehaviorsNotified;

    public ulong AllocationGeneration { get; internal set; }

    public BoundingBox BoundingBox;

    private int[]? _cells;

    private long _geometryHaloRevision;

    /// <summary>Replica geometry edits within the border read by nearby terrain meshes.</summary>
    public long GeometryHaloRevision => Volatile.Read(ref _geometryHaloRevision);

    public static bool IsInsideGeometryHalo(int x, int z) =>
        (x & SizeMinusOne) is < 2 or > 13 || (z & SizeMinusOne) is < 2 or > 13;

    /// <summary>Mutable cell storage, acquired under this terrain's access ownership.</summary>
    public int[] Cells
    {
        get => _cells ??= new int[_size * _size * _height];
        set
        {
            _cells = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateGeometryHalo();
        }
    }

    public long CellStorageBytes => (long)(_cells?.Length ?? 0) * sizeof(int);

    public Vector2 Center;

    public Point2 Coords;

    private bool _hasContents;

    /// <summary>Published cells are available independently of derived height and lighting.</summary>
    public bool HasContents
    {
        get => Volatile.Read(ref _hasContents);
        set
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Volatile.Write(ref _hasContents, value);
        }
    }

    private bool _contentInitialized;

    /// <summary>Initial business normalization finished; contents can be used as a network baseline.</summary>
    public bool IsContentInitialized
    {
        get => Volatile.Read(ref _contentInitialized);
        internal set => Volatile.Write(ref _contentInitialized, value);
    }

    public TerrainContentOrigin ContentOrigin { get; internal set; }

    private TerrainLightPresentationBatch? _lightingPresentationBatch;

    public TerrainLightPresentationBatch? LightingPresentationBatch
    {
        get => Volatile.Read(ref _lightingPresentationBatch);
        internal set => Volatile.Write(ref _lightingPresentationBatch, value);
    }

    public bool HasPendingLightingPresentation => LightingPresentationBatch?.IsPending == true;

    public TerrainDerivedRevisions DerivedRevisions { get; } = new();

    public bool HasCurrentHeightData => HasContents && DerivedRevisions.HasCurrentHeights;

    public bool HasCurrentLight => HasContents && DerivedRevisions.HasCurrentLight;

    public bool IsRequested;

    public bool NeedsContentSnapshot;

    public bool NeedsContentValidation;

    public double NetworkRequestTime;

    internal long lightRequestContentVersion;

    internal double lightRequestTime;

    public long NetworkContentVersion;

    public int ModificationCounter;

    private long _networkContentRevision;

    // Replaced atomically by the main thread; geometry workers only read snapshots.
    private Dictionary<int, (int Expected, int Value)>? _geometryPredictions;

    public long NetworkContentRevision
    {
        get => Volatile.Read(ref _networkContentRevision);
        internal init => _networkContentRevision = value;
    }

    public Point2 Origin;

    public long[] Shafts
    {
        get;
        set
        {
            field = value;
            InvalidateGeometryHalo();
        }
    } = new long[_size * _size];

    private void InvalidateGeometryHalo() => Interlocked.Increment(ref _geometryHaloRevision);

    private void TrackGeometryEdit(int x, int z)
    {
        if (ContentOrigin == TerrainContentOrigin.Replica && IsInsideGeometryHalo(x, z))
        {
            InvalidateGeometryHalo();
        }
    }

    private void TrackGeometryCellEdit(int index) =>
        TrackGeometryEdit(index >> HeightBits, index >> (HeightBits + SizeBits));

    public long StartTime;

    public readonly Terrain Terrain;

    public TerrainChunk(Terrain terrain, int x, int z)
    {
        Terrain = terrain;
        Coords = new Point2(x, z);
        Origin = new Point2(x * _size, z * _size);
        BoundingBox = new BoundingBox(
            new Vector3(Origin.X, 0f, Origin.Y),
            new Vector3(Origin.X + _size, 256f, Origin.Y + _size)
        );
        Center = new Vector2(Origin.X + 8f, Origin.Y + 8f);
    }

    public bool IsDisposed { get; private set; }

    public void Dispose()
    {
        if (IsDisposed) { return; }

        IsDisposed = true;
        Volatile.Write(ref _hasContents, false);
        Volatile.Write(ref _contentInitialized, false);
        DerivedRevisions.InvalidateHeightsAndLight();
        Volatile.Write(ref _geometryPredictions, null);
        Terrain.Lighting.Release(this);
    }

    public static bool IsCellValid(int x, int y, int z)
    {
        if (x is >= 0 and < _size && y is >= 0 and < _height && z >= 0)
        {
            return z < _size;
        }

        return false;
    }

    public static bool IsShaftValid(int x, int z)
    {
        if (x is >= 0 and < _size && z >= 0)
        {
            return z < _size;
        }

        return false;
    }

    public static int CalculateCellIndex(int x, int y, int z)
    {
        return y + x * _height + z * _height * _size;
    }

    public int CalculateTopmostCellHeight(int x, int z)
    {
        var cellIndex = CalculateCellIndex(x, _heightMinusOne, z);
        var localHeightMinusOne = _heightMinusOne;
        while (localHeightMinusOne >= 0)
        {
            if (Terrain.ExtractContents(GetCellValueFast(cellIndex)) != 0)
            {
                return localHeightMinusOne;
            }

            localHeightMinusOne--;
            cellIndex--;
        }

        return 0;
    }

    public int GetCellValueFast(int index)
    {
        if (_cells is { } cells) { return cells[index]; }

        if ((uint)index >= _size * _size * _height) { throw new IndexOutOfRangeException(); }

        return 0;
    }

    public int GetCellValueForGeometry(int index)
    {
        var value = GetCellValueFast(index);
        var predictions = Volatile.Read(ref _geometryPredictions);
        return predictions is not null &&
               predictions.TryGetValue(index, out var prediction) &&
               Terrain.NormalizeCellValue(value) == prediction.Expected
            ? Terrain.NormalizeCellValue(prediction.Value)
            : value;
    }

    public int GetCellValueForGeometry(int x, int y, int z) =>
        GetCellValueForGeometry(CalculateCellIndex(x, y, z));

    public void SetGeometryPrediction(int index, int expected, int value)
    {
        var snapshot = _geometryPredictions is { } current
            ? new Dictionary<int, (int Expected, int Value)>(current)
            : [];
        snapshot[index] = (Terrain.NormalizeCellValue(expected), value);
        Volatile.Write(ref _geometryPredictions, snapshot);
        TrackGeometryCellEdit(index);
    }

    public void RemoveGeometryPrediction(int index)
    {
        if (_geometryPredictions is not { } current || !current.ContainsKey(index))
        {
            return;
        }

        var snapshot = new Dictionary<int, (int Expected, int Value)>(current);
        snapshot.Remove(index);
        Volatile.Write(ref _geometryPredictions, snapshot.Count == 0 ? null : snapshot);
        TrackGeometryCellEdit(index);
    }

    public int GetCellValueFast(int x, int y, int z)
    {
        return GetCellValueFast(y + x * _height + z * _height * _size);
    }

    public void SetCellValueFast(int x, int y, int z, int value)
    {
        SetCellValueFast(y + x * _height + z * _height * _size, value);
    }

    public void SetCellValueFast(int index, int value)
    {
        value = Terrain.NormalizeCellValue(value);
        if (Terrain.NormalizeCellValue(GetCellValueFast(index)) != value)
        {
            Interlocked.Increment(ref _networkContentRevision);
            TrackGeometryCellEdit(index);
        }

        if (_cells != null || value != 0) { Cells[index] = value; }
    }

    public int GetCellContentsFast(int x, int y, int z)
    {
        return Terrain.ExtractContents(GetCellValueFast(x, y, z));
    }

    public long GetShaftValueFast(int x, int z)
    {
        return Shafts[x + z * _size];
    }

    public void SetShaftValueFast(int x, int z, long value)
    {
        var index = x + z * _size;
        var previous = Shafts[index];
        if (previous != value) { TrackGeometryEdit(x, z); }

        if (Terrain.ExtractTemperature(previous) != Terrain.ExtractTemperature(value) ||
            Terrain.ExtractHumidity(previous) != Terrain.ExtractHumidity(value))
        {
            Interlocked.Increment(ref _networkContentRevision);
        }

        Shafts[index] = value;
    }

    public int GetTemperatureFast(int x, int z)
    {
        return Terrain.ExtractTemperature(GetShaftValueFast(x, z));
    }

    public void SetTemperatureFast(int x, int z, int temperature)
    {
        SetShaftValueFast(x, z, Terrain.ReplaceTemperature(GetShaftValueFast(x, z), temperature));
    }

    public int GetHumidityFast(int x, int z)
    {
        return Terrain.ExtractHumidity(GetShaftValueFast(x, z));
    }

    public void SetHumidityFast(int x, int z, int humidity)
    {
        SetShaftValueFast(x, z, Terrain.ReplaceHumidity(GetShaftValueFast(x, z), humidity));
    }

    public int GetTopHeightFast(int x, int z)
    {
        return Terrain.ExtractTopHeight(GetShaftValueFast(x, z));
    }

    public void SetTopHeightFast(int x, int z, int topHeight)
    {
        SetShaftValueFast(x, z, Terrain.ReplaceTopHeight(GetShaftValueFast(x, z), topHeight));
    }

    public int GetBottomHeightFast(int x, int z)
    {
        return Terrain.ExtractBottomHeight(GetShaftValueFast(x, z));
    }

    public void SetBottomHeightFast(int x, int z, int bottomHeight)
    {
        SetShaftValueFast(x, z, Terrain.ReplaceBottomHeight(GetShaftValueFast(x, z), bottomHeight));
    }
}
