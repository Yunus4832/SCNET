using Engine.Graphics;

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

    public readonly DynamicArray<TerrainChunkGeometry.Buffer> Buffers = [];

    public int[] Cells = new int[_size * _size * _height];

    public Vector2 Center;

    public TerrainGeometry[] ChunkSliceGeometries = new TerrainGeometry[SlicesCount];

    public Point2 Coords;

    public float DrawDistanceSquared;

    public readonly Dictionary<Texture2D, TerrainGeometry[]> Draws = new();

    public readonly int[] GeneratedSliceContentsHashes = new int[SlicesCount];

    public readonly TerrainChunkGeometry Geometry = new();

    public readonly Dictionary<int, float> HazeEnds = new();

    public bool IsLoaded;

    public bool IsRequested;

    public double NetworkRequestTime;

    public int NetworkRequestAttempts;

    public double NetworkContentReceiveTime;

    public double NetworkGeometryReadyTime;

    public double NetworkGeometryUploadTime;

    public bool NetworkGeometryUploaded;

    public long NetworkContentVersion;

    public long ClientGeometryContentVersion;

    public int LightPropagationMask;

    public int ModificationCounter;

    private long _networkContentRevision;

    public long NetworkContentRevision => Volatile.Read(ref _networkContentRevision);

    public volatile bool NewGeometryData;

    public Point2 Origin;

    public long[] Shafts = new long[_size * _size];

    public readonly int[] SliceContentsHashes = new int[SlicesCount];

    public long StartTime;

    /// <summary>State consumed by gameplay and rendering on the main thread.</summary>
    public TerrainChunkState MainThreadState { get; internal set; }

    public readonly Terrain Terrain;

    private int _queuedWorkerDowngrade = -1;

    private int _publishedWorkerState = -1;

    public TerrainChunk(Terrain terrain, int x, int z)
    {
        Terrain = terrain;
        Coords = new Point2(x, z);
        Origin = new Point2(x * _size, z * _size);
        BoundingBox = new BoundingBox(new Vector3(Origin.X, 0f, Origin.Y),
            new Vector3(Origin.X + _size, 256f, Origin.Y + _size));
        Center = new Vector2(Origin.X + 8f, Origin.Y + 8f);
    }

    /// <summary>State owned by the terrain worker or an isolated authority generation pipeline.</summary>
    public TerrainChunkState WorkerState { get; internal set; }

    internal bool HasQueuedWorkerDowngrade => Volatile.Read(ref _queuedWorkerDowngrade) >= 0;

    internal void QueueWorkerDowngrade(TerrainChunkState state)
    {
        var requested = (int)state;
        while (true)
        {
            var current = Volatile.Read(ref _queuedWorkerDowngrade);
            var desired = current < 0 ? requested : Math.Min(current, requested);
            if (current == desired ||
                Interlocked.CompareExchange(ref _queuedWorkerDowngrade, desired, current) == current)
            {
                return;
            }
        }
    }

    internal bool TryConsumeWorkerDowngrade(out TerrainChunkState state)
    {
        var value = Interlocked.Exchange(ref _queuedWorkerDowngrade, -1);
        state = value >= 0 ? (TerrainChunkState)value : default;
        return value >= 0;
    }

    internal void PublishWorkerState(TerrainChunkState state) =>
        Interlocked.Exchange(ref _publishedWorkerState, (int)state);

    internal bool TryConsumePublishedWorkerState(out TerrainChunkState state)
    {
        var value = Interlocked.Exchange(ref _publishedWorkerState, -1);
        state = value >= 0 ? (TerrainChunkState)value : default;
        return value >= 0;
    }

    internal void DiscardPublishedWorkerState() =>
        Interlocked.Exchange(ref _publishedWorkerState, -1);

    internal void ResetStateExchange()
    {
        Interlocked.Exchange(ref _queuedWorkerDowngrade, -1);
        Interlocked.Exchange(ref _publishedWorkerState, -1);
    }

    public void Dispose()
    {
        foreach (var buffer in Buffers)
        {
            buffer.Dispose();
        }

        var drawArray = Draws.ToArray();
        foreach (var draw in drawArray)
        {
            var drawValues = draw.Value.ToArray();
            foreach (var value in drawValues)
            {
                value.Dispose();
            }
        }
    }

    public void InvalidateSliceContentsHashes()
    {
        for (var i = 0; i < GeneratedSliceContentsHashes.Length; i++)
        {
            GeneratedSliceContentsHashes[i] = 0;
        }
    }

    public void CopySliceContentsHashes()
    {
        for (var i = 0; i < GeneratedSliceContentsHashes.Length; i++)
        {
            GeneratedSliceContentsHashes[i] = SliceContentsHashes[i];
        }
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
        return Cells[index];
    }

    public int GetCellValueFast(int x, int y, int z)
    {
        return Cells[y + x * _height + z * _height * _size];
    }

    public void SetCellValueFast(int x, int y, int z, int value)
    {
        SetCellValueFast(y + x * _height + z * _height * _size, value);
    }

    public void SetCellValueFast(int index, int value)
    {
        if (Terrain.ReplaceLight(Cells[index], 0) != Terrain.ReplaceLight(value, 0))
        {
            Interlocked.Increment(ref _networkContentRevision);
        }

        Cells[index] = value;
    }

    public int GetCellContentsFast(int x, int y, int z)
    {
        return Terrain.ExtractContents(GetCellValueFast(x, y, z));
    }

    public int GetCellLightFast(int x, int y, int z)
    {
        return Terrain.ExtractLight(GetCellValueFast(x, y, z));
    }

    public long GetShaftValueFast(int x, int z)
    {
        return Shafts[x + z * _size];
    }

    public void SetShaftValueFast(int x, int z, long value)
    {
        var index = x + z * _size;
        var previous = Shafts[index];
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

    public int GetSunlightHeightFast(int x, int z)
    {
        return Terrain.ExtractSunlightHeight(GetShaftValueFast(x, z));
    }

    public void SetSunlightHeightFast(int x, int z, int sunlightHeight)
    {
        SetShaftValueFast(x, z, Terrain.ReplaceSunlightHeight(GetShaftValueFast(x, z), sunlightHeight));
    }
}
