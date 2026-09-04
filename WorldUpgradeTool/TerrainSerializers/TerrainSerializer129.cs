using Game;

namespace WorldUpgradeTool.TerrainSerializers;

public class TerrainSerializer129 : IDisposable
{
    public const int MaxChunks = 65536;

    public const int TocEntryBytesCount = 12;

    public const int TocBytesCount = 786444;

    public const int ChunkSizeX = 16;

    public const int ChunkSizeY = 128;

    public const int ChunkSizeZ = 16;

    public const int ChunkBitsX = 4;

    public const int ChunkBitsZ = 4;

    public const int ChunkBytesCount = 132112;

    public const string ChunksFileName = "Chunks32.dat";

    private readonly byte[] _buffer = new byte[131072];

    private readonly Dictionary<Point2, long> _chunkOffsets = new();

    private Stream _stream;

    private readonly Terrain _terrain;

    public TerrainSerializer129(Terrain terrain, string directoryName)
    {
        _terrain = terrain;
        var path = Storage.CombinePaths(directoryName, "Chunks32.dat");
        if (!Storage.FileExists(path))
        {
            using var stream = Storage.OpenFile(path, OpenFileMode.Create);
            for (var i = 0; i < 65537; i++)
            {
                WriteTocEntry(stream, 0, 0, -1);
            }
        }

        _stream = Storage.OpenFile(path, OpenFileMode.ReadWrite);
        while (true)
        {
            ReadTocEntry(_stream, out var cx, out var cz, out var index);
            if (index >= 0)
            {
                _chunkOffsets[new Point2(cx, cz)] = 786444 + 132112L * index;
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
        if (chunk is not { State: > TerrainChunkState.InvalidContents4, ModificationCounter: > 0 })
        {
            return;
        }

        SaveChunkBlocks(chunk);
        chunk.ModificationCounter = 0;
    }

    public static void ReadChunkHeader(Stream stream)
    {
        var num = ReadInt(stream);
        var num2 = ReadInt(stream);
        ReadInt(stream);
        ReadInt(stream);
        if (num != -559038737 || num2 != -2)
        {
            throw new InvalidOperationException("Invalid chunk header.");
        }
    }

    public static void WriteChunkHeader(Stream stream, int cx, int cz)
    {
        WriteInt(stream, -559038737);
        WriteInt(stream, -2);
        WriteInt(stream, cx);
        WriteInt(stream, cz);
    }

    public static void ReadTocEntry(Stream stream, out int cx, out int cz, out int index)
    {
        cx = ReadInt(stream);
        cz = ReadInt(stream);
        index = ReadInt(stream);
    }

    public static void WriteTocEntry(Stream stream, int cx, int cz, int index)
    {
        WriteInt(stream, cx);
        WriteInt(stream, cz);
        WriteInt(stream, index);
    }

    public bool LoadChunkBlocks(TerrainChunk chunk)
    {
        var result = false;
        var num = chunk.Origin.X >> 4;
        var num2 = chunk.Origin.Y >> 4;
        try
        {
            if (!_chunkOffsets.TryGetValue(new Point2(num, num2), out var value))
            {
                return result;
            }

            _ = Time.RealTime;
            _stream.Seek(value, SeekOrigin.Begin);
            ReadChunkHeader(_stream);
            _stream.ReadExactly(_buffer, 0, 131072);
            var intBufferForCells = new int[131072 / sizeof(int)];
            Buffer.BlockCopy(_buffer, 0, intBufferForCells, 0, 131072);
            var index = 0;
            for (var i = 0; i < 16; i++)
            {
                for (var j = 0; j < 16; j++)
                {
                    var num3 = TerrainChunk.CalculateCellIndex(i, 0, j);
                    var num4 = 0;
                    while (num4 < 128)
                    {
                        chunk.SetCellValueFast(num3, intBufferForCells[index]);
                        num4++;
                        num3++;
                        index++;
                    }
                }
            }

            _stream.ReadExactly(_buffer, 0, 1024);
            var intBufferForShafts = new int[1024 / sizeof(int)];
            Buffer.BlockCopy(_buffer, 0, intBufferForShafts, 0, 1024);
            var shaftIndex = 0;
            for (var k = 0; k < 16; k++)
            {
                for (var l = 0; l < 16; l++)
                {
                    _terrain.SetShaftValue(k + chunk.Origin.X, l + chunk.Origin.Y, intBufferForShafts[shaftIndex]);
                    shaftIndex++;
                }
            }

            result = true;
            _ = Time.RealTime;
            return result;
        }
        catch (Exception e)
        {
            Log.Error(ExceptionManager.MakeFullErrorMessage($"Error loading data for chunk ({num},{num2}).", e));
            return result;
        }
    }

    public void SaveChunkBlocks(TerrainChunk chunk)
    {
        _ = Time.RealTime;
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
                value = _stream.Length;
                _stream.Seek(value, SeekOrigin.Begin);
            }

            WriteChunkHeader(_stream, num, num2);
            var intBufferForCells = new int[16 * 16 * 128 * sizeof(int) / sizeof(int)];
            var index = 0;
            for (var i = 0; i < 16; i++)
            {
                for (var j = 0; j < 16; j++)
                {
                    var num3 = TerrainChunk.CalculateCellIndex(i, 0, j);
                    var num4 = 0;
                    while (num4 < 128)
                    {
                        intBufferForCells[index] = chunk.GetCellValueFast(num3);
                        num4++;
                        num3++;
                        index++;
                    }
                }
            }

            Buffer.BlockCopy(intBufferForCells, 0, _buffer, 0, 131072);
            _stream.Write(_buffer, 0, 131072);

            var intBufferForShafts = new int[16 * 16 * sizeof(int) / sizeof(int)]; // 1024 bytes / 4 = 256 ints
            var shaftIndex = 0;
            for (var k = 0; k < 16; k++)
            {
                for (var l = 0; l < 16; l++)
                {
                    intBufferForShafts[shaftIndex] = (int)_terrain.GetShaftValue(k + chunk.Origin.X, l + chunk.Origin.Y);
                    shaftIndex++;
                }
            }

            Buffer.BlockCopy(intBufferForShafts, 0, _buffer, 0, 1024);
            _stream.Write(_buffer, 0, 1024);
            if (flag)
            {
                _stream.Flush();
                var num5 = _chunkOffsets.Count % 65536 * 3 * 4;
                _stream.Seek(num5, SeekOrigin.Begin);
                WriteInt(_stream, num);
                WriteInt(_stream, num2);
                WriteInt(_stream, _chunkOffsets.Count);
                _chunkOffsets[new Point2(num, num2)] = value;
            }

            _stream.Flush();
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
        stream.WriteByte((byte)value);
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 24));
    }
}
