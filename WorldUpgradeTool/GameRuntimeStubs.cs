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
        private const int ContentsMask = 0x3FF;
        private const int LightMask = 0x3C00;
        private const int LightShift = 10;
        private const int DataMask = unchecked((int)0xFFFFC000);
        private const int DataShift = 14;
        private const long TopHeightMask = 0xFFF;
        private const int TopHeightShift = 0;
        private const long TemperatureMask = 0xF000;
        private const int TemperatureShift = 12;
        private const long HumidityMask = 0xF0000;
        private const int HumidityShift = 16;
        private const long BottomHeightMask = 0xFFF00000;
        private const int BottomHeightShift = 20;
        private const long SunlightHeightMask = 0xFFF00000000;
        private const int SunlightHeightShift = 32;

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
            return value & ContentsMask;
        }

        public static int ExtractLight(int value)
        {
            return (value & LightMask) >> LightShift;
        }

        public static int ExtractData(int value)
        {
            return (value & DataMask) >> DataShift;
        }

        public static int ExtractTopHeight(long value)
        {
            return (int)((value & TopHeightMask) >> TopHeightShift);
        }

        public static int ExtractBottomHeight(long value)
        {
            return (int)((value & BottomHeightMask) >> BottomHeightShift);
        }

        public static int ExtractSunlightHeight(long value)
        {
            return (int)((value & SunlightHeightMask) >> SunlightHeightShift);
        }

        public static int ExtractHumidity(long value)
        {
            return (int)((value & HumidityMask) >> HumidityShift);
        }

        public static int ExtractTemperature(long value)
        {
            return (int)((value & TemperatureMask) >> TemperatureShift);
        }

        public static int ReplaceContents(int value, int contents)
        {
            return (value & ~ContentsMask) | (contents & ContentsMask);
        }

        public static int ReplaceLight(int value, int light)
        {
            return (value & ~LightMask) | ((light << LightShift) & LightMask);
        }

        public static int ReplaceData(int value, int data)
        {
            return (value & ~DataMask) | ((data << DataShift) & DataMask);
        }

        public static long ReplaceTopHeight(long value, int topHeight)
        {
            return (value & ~TopHeightMask) | (((long)topHeight << TopHeightShift) & TopHeightMask);
        }

        public static long ReplaceBottomHeight(long value, int bottomHeight)
        {
            return (value & ~BottomHeightMask) | (((long)bottomHeight << BottomHeightShift) & BottomHeightMask);
        }

        public static long ReplaceSunlightHeight(long value, int sunlightHeight)
        {
            return (value & ~SunlightHeightMask) | (((long)sunlightHeight << SunlightHeightShift) & SunlightHeightMask);
        }

        public static long ReplaceHumidity(long value, int humidity)
        {
            return (value & ~HumidityMask) | (((long)humidity << HumidityShift) & HumidityMask);
        }

        public static long ReplaceTemperature(long value, int temperature)
        {
            return (value & ~TemperatureMask) | (((long)temperature << TemperatureShift) & TemperatureMask);
        }
    }

    public sealed class TerrainChunk
    {
        private const int Size = 16;
        private const int Height = 256;

        private readonly int[] _cells = new int[Size * Height * Size];
        private readonly long[] _shafts = new long[Size * Size];

        public TerrainChunk(Terrain? terrain, int x, int z)
        {
            Terrain = terrain ?? new Terrain();
            Coords = new Point2(x, z);
            Origin = new Point2(x * Size, z * Size);
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
            return y + x * Height + z * Height * Size;
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
            return _shafts[x + z * Size];
        }

        public void SetShaftValueFast(int x, int z, long value)
        {
            _shafts[x + z * Size] = value;
        }
    }
}

namespace Game.Subsystems
{
    public sealed class SubsystemTerrain
    {
        public Game.Terrains.Terrain Terrain { get; } = new();
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
