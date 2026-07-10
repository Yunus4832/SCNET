namespace Game
{
    public enum TerrainChunkState
    {
        NotLoaded,
        InvalidContents1,
        InvalidContents2,
        InvalidContents3,
        InvalidContents4,
        InvalidLight,
        InvalidPropagatedLight,
        InvalidVertices1,
        InvalidVertices2,
        Valid
    }
}

namespace Game.Terrains
{
    public sealed class Terrain
    {
        private const int _contentsMask = 0x3FF;
        private const int _lightMask = 0x3C00;
        private const int _lightShift = 10;
        private const int _dataMask = unchecked((int)0xFFFFC000);
        private const int _dataShift = 14;
        private const long _topHeightMask = 0xFFF;
        private const int _topHeightShift = 0;
        private const long _temperatureMask = 0xF000;
        private const int _temperatureShift = 12;
        private const long _humidityMask = 0xF0000;
        private const int _humidityShift = 16;
        private const long _bottomHeightMask = 0xFFF00000;
        private const int _bottomHeightShift = 20;
        private const long _sunlightHeightMask = 0xFFF00000000;
        private const int _sunlightHeightShift = 32;

        private readonly Dictionary<Point2, TerrainChunk> _chunks = new();

        public long GetShaftValue(int x, int z)
        {
            return GetChunkAtCell(x, z)?.GetShaftValueFast(x & 0xF, z & 0xF) ?? 0;
        }

        public void SetShaftValue(int x, int z, long value)
        {
            GetChunkAtCell(x, z)?.SetShaftValueFast(x & 0xF, z & 0xF, value);
        }

        internal void RegisterChunk(TerrainChunk chunk)
        {
            _chunks[chunk.Coords] = chunk;
        }

        private TerrainChunk? GetChunkAtCell(int x, int z)
        {
            _chunks.TryGetValue(new Point2(x >> 4, z >> 4), out var chunk);
            return chunk;
        }

        public static int ExtractContents(int value)
        {
            return value & _contentsMask;
        }

        public static int ExtractLight(int value)
        {
            return (value & _lightMask) >> _lightShift;
        }

        public static int ExtractData(int value)
        {
            return (value & _dataMask) >> _dataShift;
        }

        public static int ExtractTopHeight(long value)
        {
            return (int)((value & _topHeightMask) >> _topHeightShift);
        }

        public static int ExtractBottomHeight(long value)
        {
            return (int)((value & _bottomHeightMask) >> _bottomHeightShift);
        }

        public static int ExtractSunlightHeight(long value)
        {
            return (int)((value & _sunlightHeightMask) >> _sunlightHeightShift);
        }

        public static int ExtractHumidity(long value)
        {
            return (int)((value & _humidityMask) >> _humidityShift);
        }

        public static int ExtractTemperature(long value)
        {
            return (int)((value & _temperatureMask) >> _temperatureShift);
        }

        public static int ReplaceContents(int value, int contents)
        {
            return (value & ~_contentsMask) | (contents & _contentsMask);
        }

        public static int ReplaceLight(int value, int light)
        {
            return (value & ~_lightMask) | ((light << _lightShift) & _lightMask);
        }

        public static int ReplaceData(int value, int data)
        {
            return (value & ~_dataMask) | ((data << _dataShift) & _dataMask);
        }

        public static long ReplaceTopHeight(long value, int topHeight)
        {
            return (value & ~_topHeightMask) | (((long)topHeight << _topHeightShift) & _topHeightMask);
        }

        public static long ReplaceBottomHeight(long value, int bottomHeight)
        {
            return (value & ~_bottomHeightMask) | (((long)bottomHeight << _bottomHeightShift) & _bottomHeightMask);
        }

        public static long ReplaceSunlightHeight(long value, int sunlightHeight)
        {
            return (value & ~_sunlightHeightMask) | (((long)sunlightHeight << _sunlightHeightShift) & _sunlightHeightMask);
        }

        public static long ReplaceHumidity(long value, int humidity)
        {
            return (value & ~_humidityMask) | (((long)humidity << _humidityShift) & _humidityMask);
        }

        public static long ReplaceTemperature(long value, int temperature)
        {
            return (value & ~_temperatureMask) | (((long)temperature << _temperatureShift) & _temperatureMask);
        }
    }

    public sealed class TerrainChunk
    {
        private const int _size = 16;
        private const int _height = 256;

        private readonly int[] _cells = new int[_size * _height * _size];
        private readonly long[] _shafts = new long[_size * _size];

        public TerrainChunk(Terrain? terrain, int x, int z)
        {
            Terrain = terrain ?? new Terrain();
            Coords = new Point2(x, z);
            Origin = new Point2(x * _size, z * _size);
            State = TerrainChunkState.Valid;
            ModificationCounter = 1;
            Terrain.RegisterChunk(this);
        }

        public Point2 Coords { get; }

        public Point2 Origin { get; }

        public int ModificationCounter { get; set; }

        public TerrainChunkState State { get; set; }

        public Terrain Terrain { get; }

        public static int CalculateCellIndex(int x, int y, int z)
        {
            return y + x * _height + z * _height * _size;
        }

        public int GetCellValueFast(int index)
        {
            return _cells[index];
        }

        public int GetCellValueFast(int x, int y, int z)
        {
            return _cells[CalculateCellIndex(x, y, z)];
        }

        public void SetCellValueFast(int index, int value)
        {
            _cells[index] = value;
        }

        public void SetCellValueFast(int x, int y, int z, int value)
        {
            _cells[CalculateCellIndex(x, y, z)] = value;
        }

        public long GetShaftValueFast(int x, int z)
        {
            return _shafts[x + z * _size];
        }

        public void SetShaftValueFast(int x, int z, long value)
        {
            _shafts[x + z * _size] = value;
        }
    }
}

namespace Game.Subsystems
{
    public sealed class SubsystemTerrain
    {
        public Terrain Terrain { get; } = new();
    }

    public static class SubsystemSeasons
    {
        public const float SummerStart = 0.375f;
        public const float AutumnStart = 0.625f;
    }
}

namespace Game.Utils
{
    public static class IntervalUtils
    {
        public static float Midpoint(float start, float end)
        {
            return start <= end ? (start + end) / 2f : MathUtils.Remainder((start + end + 1f) / 2f, 1f);
        }
    }
}

namespace Game.Dialogs
{
    public sealed class BusyDialog
    {
        public BusyDialog(string title, string message)
        {
        }
    }

    public sealed class MessageDialog
    {
        public MessageDialog(
            string largeMessage,
            string smallMessage,
            string button1Text,
            string button2Text,
            Action handler)
        {
        }
    }
}

namespace Game.Managers
{
    public static class DialogsManager
    {
        public static void ShowDialog(object? parent, object dialog)
        {
        }

        public static void HideDialog(object dialog)
        {
        }
    }

    public static class SettingsManager
    {
        public static void LoadSettings()
        {
        }
    }

    public static class ExceptionManager
    {
        public static string MakeFullErrorMessage(string message, Exception exception)
        {
            return message + Environment.NewLine + exception;
        }
    }
}
