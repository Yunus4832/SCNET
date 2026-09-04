using Game;

namespace WorldUpgradeTool.TerrainSerializers;

public class TerrainSerializer14 : IDisposable
{
    public const int MaxChunks = 65536;

    public const string ChunksFileName = "Chunks.dat";

    private readonly byte[] _buffer = new byte[131072];

    private readonly Dictionary<Point2, int> _chunkOffsets = new();

    private Stream _stream;

    private readonly SubsystemTerrain _subsystemTerrain;

    public TerrainSerializer14(SubsystemTerrain subsystemTerrain, string directoryName)
    {
        _subsystemTerrain = subsystemTerrain;
        var path = Storage.CombinePaths(directoryName, "Chunks.dat");
        if (!Storage.FileExists(path))
        {
            using var stream = Storage.OpenFile(path, OpenFileMode.Create);
            for (var i = 0; i < 65537; i++)
            {
                WriteTocEntry(stream, 0, 0, 0);
            }
        }

        _stream = Storage.OpenFile(path, OpenFileMode.ReadWrite);
        while (true)
        {
            ReadTocEntry(_stream, out var cx, out var cz, out var offset);
            if (offset != 0)
            {
                _chunkOffsets[new Point2(cx, cz)] = offset;
                continue;
            }

            break;
        }
    }

    public void Dispose()
    {
        Utilities.Dispose(ref _stream!);
    }

    public bool LoadChunk(TerrainChunk chunk)
    {
        return LoadChunkBlocks(chunk);
    }

    public void SaveChunk(TerrainChunk chunk)
    {
        if (chunk.State > TerrainChunkState.InvalidContents4 && chunk.ModificationCounter > 0)
        {
            SaveChunkBlocks(chunk);
            chunk.ModificationCounter = 0;
        }
    }

    public static void ReadChunkHeader(Stream stream)
    {
        var num = ReadInt(stream);
        var num2 = ReadInt(stream);
        ReadInt(stream);
        ReadInt(stream);
        if (num != -559038737 || num2 != -1)
        {
            throw new InvalidOperationException("Invalid chunk header.");
        }
    }

    public static void WriteChunkHeader(Stream stream, int cx, int cz)
    {
        WriteInt(stream, -559038737);
        WriteInt(stream, -1);
        WriteInt(stream, cx);
        WriteInt(stream, cz);
    }

    public static void ReadTocEntry(Stream stream, out int cx, out int cz, out int offset)
    {
        cx = ReadInt(stream);
        cz = ReadInt(stream);
        offset = ReadInt(stream);
    }

    public static void WriteTocEntry(Stream stream, int cx, int cz, int offset)
    {
        WriteInt(stream, cx);
        WriteInt(stream, cz);
        WriteInt(stream, offset);
    }

    public bool LoadChunkBlocks(TerrainChunk chunk)
    {
        _ = Time.RealTime;
        var result = false;
        var terrain = _subsystemTerrain.Terrain;
        var num = chunk.Origin.X >> 4;
        var num2 = chunk.Origin.Y >> 4;
        try
        {
            if (_chunkOffsets.TryGetValue(new Point2(num, num2), out var value))
            {
                _stream.Seek(value, SeekOrigin.Begin);
                ReadChunkHeader(_stream);
                var num3 = 0;
                _stream.ReadExactly(_buffer, 0, 131072);
                for (var i = 0; i < 16; i++)
                {
                    for (var j = 0; j < 16; j++)
                    {
                        var num4 = TerrainChunk.CalculateCellIndex(i, 0, j);
                        for (var k = 0; k < 256; k++)
                        {
                            int num5 = _buffer[num3++];
                            num5 |= _buffer[num3++] << 8;
                            chunk.SetCellValueFast(num4++, num5);
                        }
                    }
                }

                num3 = 0;
                _stream.ReadExactly(_buffer, 0, 1024);
                for (var l = 0; l < 16; l++)
                {
                    for (var m = 0; m < 16; m++)
                    {
                        int num6 = _buffer[num3++];
                        num6 |= _buffer[num3++] << 8;
                        num6 |= _buffer[num3++] << 16;
                        num6 |= _buffer[num3++] << 24;
                        terrain.SetShaftValue(l + chunk.Origin.X, m + chunk.Origin.Y, num6);
                    }
                }

                result = true;
            }
        }
        catch (Exception e)
        {
            Log.Error(ExceptionManager.MakeFullErrorMessage($"Error loading data for chunk ({num},{num2}).", e));
        }

        _ = Time.RealTime;
        return result;
    }

    public void SaveChunkBlocks(TerrainChunk chunk)
    {
        _ = Time.RealTime;
        var terrain = _subsystemTerrain.Terrain;
        var num = chunk.Origin.X >> 4;
        var num2 = chunk.Origin.Y >> 4;
        try
        {
            var flag = false;
            if (_chunkOffsets.TryGetValue(new Point2(num, num2), out var value))
            {
                _stream.Seek(value, SeekOrigin.Begin);
            }
            else
            {
                flag = true;
                value = (int)_stream.Length;
                _stream.Seek(value, SeekOrigin.Begin);
            }

            WriteChunkHeader(_stream, num, num2);
            var num3 = 0;
            for (var i = 0; i < 16; i++)
            {
                for (var j = 0; j < 16; j++)
                {
                    var num4 = TerrainChunk.CalculateCellIndex(i, 0, j);
                    for (var k = 0; k < 256; k++)
                    {
                        var cellValueFast = chunk.GetCellValueFast(num4++);
                        _buffer[num3++] = (byte)cellValueFast;
                        _buffer[num3++] = (byte)(cellValueFast >> 8);
                    }
                }
            }

            _stream.Write(_buffer, 0, 131072);
            num3 = 0;
            for (var l = 0; l < 16; l++)
            {
                for (var m = 0; m < 16; m++)
                {
                    var shaftValue = terrain.GetShaftValue(l + chunk.Origin.X, m + chunk.Origin.Y);
                    _buffer[num3++] = (byte)shaftValue;
                    _buffer[num3++] = (byte)(shaftValue >> 8);
                    _buffer[num3++] = (byte)(shaftValue >> 16);
                    _buffer[num3++] = (byte)(shaftValue >> 24);
                }
            }

            _stream.Write(_buffer, 0, 1024);
            if (flag)
            {
                _stream.Flush();
                var num5 = _chunkOffsets.Count % 65536 * 3 * 4;
                _stream.Seek(num5, SeekOrigin.Begin);
                WriteInt(_stream, num);
                WriteInt(_stream, num2);
                WriteInt(_stream, value);
                _chunkOffsets[new Point2(num, num2)] = value;
            }
        }
        catch (Exception e)
        {
            Log.Error(ExceptionManager.MakeFullErrorMessage($"Error writing data for chunk ({num},{num2}).", e));
        }

        _ = Time.RealTime;
    }

    public static int ReadInt(Stream stream)
    {
        return stream.ReadByte() + (stream.ReadByte() << 8) + (stream.ReadByte() << 16) + (stream.ReadByte() << 24);
    }

    public static void WriteInt(Stream stream, int value)
    {
        stream.WriteByte((byte)(value & 0xFF));
        stream.WriteByte((byte)((value >> 8) & 0xFF));
        stream.WriteByte((byte)((value >> 16) & 0xFF));
        stream.WriteByte((byte)((value >> 24) & 0xFF));
    }
}
