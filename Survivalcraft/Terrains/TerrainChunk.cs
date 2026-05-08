using Engine.Graphics;

namespace Game.Terrains;

public class TerrainChunk : IDisposable
{
    private const int _size = 16;

    private const int _height = 512;

    private const int _slicesCount = 32;

    private const int _heightMinusOne = 511;

    public const int SizeMinusOne = 15;

    public const int SizeBits = 4;

    public const int HeightBits = 9;

    public const int SliceHeight = 16;

    public bool AreBehaviorsNotified;

    public BoundingBox BoundingBox;

    public readonly DynamicArray<TerrainChunkGeometry.Buffer> Buffers = [];

    public int[] Cells = new int[_size * _size * _height];

    public Vector2 Center;

    public TerrainGeometry[] ChunkSliceGeometries = new TerrainGeometry[_slicesCount];

    public Point2 Coords;

    public TerrainChunkState? DowngradedState;

    public float DrawDistanceSquared;

    public readonly Dictionary<Texture2D, TerrainGeometry[]> Draws = new();

    public readonly int[] GeneratedSliceContentsHashes = new int[_slicesCount];

    public readonly TerrainChunkGeometry Geometry = new();

    public readonly Dictionary<int, float> HazeEnds = new();

    public bool IsLoaded;

    public bool IsRequested;

    public int LightPropagationMask;

    public int ModificationCounter;

    public volatile bool NewGeometryData;

    public Point2 Origin;

    public long[] Shafts = new long[_size * _size];

    public readonly int[] SliceContentsHashes = new int[_slicesCount];

    public long StartTime;

    public TerrainChunkState State;

    public readonly Terrain Terrain;

    public TerrainChunkState? UpgradedState;

    public bool WasDowngraded;

    public bool WasUpgraded;

    public TerrainChunk(Terrain terrain, int x, int z)
    {
        Terrain = terrain;
        Coords = new Point2(x, z);
        Origin = new Point2(x * _size, z * _size);
        BoundingBox = new BoundingBox(new Vector3(Origin.X, 0f, Origin.Y),
            new Vector3(Origin.X + _size, 512f, Origin.Y + _size));
        Center = new Vector2(Origin.X + 8f, Origin.Y + 8f);
    }

    public TerrainChunkState ThreadState { get; set; }

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
        Cells[y + x * _height + z * _height * _size] = value;
    }

    public void SetCellValueFast(int index, int value)
    {
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
        Shafts[x + z * _size] = value;
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
