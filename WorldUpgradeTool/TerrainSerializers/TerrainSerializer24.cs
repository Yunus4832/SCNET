using System.Data;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

using Game;

namespace WorldUpgradeTool.TerrainSerializers;

/// <summary>
///     版本 2.4 地形序列化工具，版本 2.4 的区块高度是 256
/// </summary>
public class TerrainSerializer24 : IDisposable
{
    /// <summary>
    ///     最大数据块大小: 一个 Cell 占 8 Byte, 一个 Chunk 数据块有 16 x 16 x 256 = 65_536 Cell
    ///     因此，最糟糕的情况下，一个未经压缩的 Chunk 数据块的大小为 65_536 x 8 = 524_800
    /// </summary>
    private const int _worstCaseChunkDataSize = 524_800;

    private readonly byte[] _compressBuffer = new byte[_worstCaseChunkDataSize];

    private readonly Lock _lock = new();

    private readonly byte[] _storageBuffer = new byte[_worstCaseChunkDataSize];

    protected IStorage storage;

    public TerrainSerializer24(string directoryName, string suffix = "")
    {
        storage = new RegionFileStorage(this);
        storage.Open(directoryName, suffix);
    }

    public TerrainSerializer24()
    {
        storage = new RegionFileStorage(this);
    }

    public virtual void Dispose()
    {
        Utilities.Dispose(ref storage!);
    }

    /// <summary>
    ///     同 LoadChunkData, 保存 Chunk 数据块
    /// </summary>
    /// <param name="chunk"> 需要保存的 Chunk 数据块对象 </param>
    /// <returns> 是否加载成功 </returns>
    public bool LoadChunk(TerrainChunk chunk)
    {
        return LoadChunkData(chunk);
    }

    /// <summary>
    ///     同 LoadChunkData 加载 Chunk 数据块, 但是增加额外的 ChunkState 校验
    /// </summary>
    /// <param name="chunk"> 加载结果 </param>
    /// <returns> 是否加载成功 </returns>
    public void SaveChunk(TerrainChunk chunk)
    {
        if (chunk.State <= TerrainChunkState.InvalidContents4 || chunk.ModificationCounter <= 0)
        {
            return;
        }

        SaveChunkData(chunk);
        chunk.ModificationCounter = 0;
    }

    /// <summary>
    ///     加载 Chunk 数据块
    /// </summary>
    /// <param name="chunk"> 加载结果 </param>
    /// <returns> 是否加载成功 </returns>
    private bool LoadChunkData(TerrainChunk chunk)
    {
        lock (_lock)
        {
            _ = Time.RealTime;
            try
            {
                var readDataByteCount = storage.Load(chunk.Coords, _storageBuffer);
                // 读取字节数小于 0，说明加载数据块失败，返回 false
                if (readDataByteCount < 0)
                {
                    return false;
                }

                // 解压数据块
                DecompressChunkData(chunk, _storageBuffer, readDataByteCount);
            }
            catch (Exception e)
            {
                Log.Error(ExceptionManager.MakeFullErrorMessage(
                    $"Error loading chunk ({chunk.Coords.X},{chunk.Coords.Y},{chunk.Origin.X},{chunk.Origin.Y}).", e));
                return false;
            }

            _ = Time.RealTime;
            return true;
        }
    }

    /// <summary>
    ///     保存 Chunk 数据块
    /// </summary>
    /// <param name="chunk"> 需要保存的 Chunk 数据块对象 </param>
    private void SaveChunkData(TerrainChunk chunk)
    {
        lock (_lock)
        {
            _ = Time.RealTime;
            try
            {
                // 先压缩获得 buffer，然后保存
                var size = CompressChunkData(chunk, _storageBuffer);
                storage.Save(chunk.Coords, _storageBuffer, size);
            }
            catch (Exception e)
            {
                Log.Error(ExceptionManager.MakeFullErrorMessage(
                    string.Format("Error saving chunk ({0},{1},{2},{3}).", chunk.Coords.X, chunk.Coords.Y,
                        chunk.Origin.X, chunk.Origin.Y), e));
            }

            _ = Time.RealTime;
        }
    }

    /// <summary>
    ///     压缩数据块
    /// </summary>
    /// <param name="chunk"> 需要压缩的数据块对象 </param>
    /// <param name="buffer"> 压缩后的数据块 buffer </param>
    /// <returns> 返回压缩后的大小 </returns>
    /// <exception cref="InvalidOperationException"></exception>
    private int CompressChunkData(TerrainChunk chunk, byte[] buffer)
    {
        // 当前缓冲区大小
        var bufferSize = 0;
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var shaftValue = chunk.GetShaftValueFast(i, j);
                // 将温度和湿度写入到 buffer
                _compressBuffer[bufferSize++] = (byte)((Terrain.ExtractTemperature(shaftValue) << 4) |
                                                       Terrain.ExtractHumidity(shaftValue));
            }
        }

        // 缓冲区大小不能大于一个 Chunk 数据块的最大大小
        if (bufferSize >= _compressBuffer.Length)
        {
            throw new InvalidOperationException("Compression buffer overflow.");
        }

        // 首先使用 RLE 算法压缩重复的数据, 版本 2.4 层高 256
        var value = -1; // 值
        var count = 0; // 值重复的次数
        for (var k = 0; k < 256; k++)
        {
            for (var l = 0; l < 16; l++)
            {
                for (var m = 0; m < 16; m++)
                {
                    // 获取下一个值, 注意: 在存档文件中，光照信息被清除并被用于存储 RLE 算法中 Cell 的重复次数
                    var nextValue = Terrain.ReplaceLight(chunk.GetCellValueFast(m, k, l), 0);
                    if (count == 0)
                    {
                        value = nextValue;
                        count = 1;
                        continue;
                    }

                    // 如果下一个值和当前值不等，说明不再重复，即找到了一个 ValueCountPair，则写入 buffer 并退出本次循环
                    if (nextValue != value)
                    {
                        bufferSize = WriteRleValueToBuffer(_compressBuffer, bufferSize, value, count);
                        value = nextValue;
                        count = 1;
                        continue;
                    }

                    count++;
                    // 重复的最大次数是 271， 到达最大值时，强制创建 ValueCountPair, 写入 buffer 并退出本次循环
                    if (count != 271)
                    {
                        continue;
                    }

                    bufferSize = WriteRleValueToBuffer(_compressBuffer, bufferSize, value, count);
                    count = 0;
                }
            }
        }

        // 可能存在最后一个 Cell 和前一个 Cell 不同的情况，此时循环已经结束，该 cell 不会写入到 buffer
        // 并且此时 count = 1, 即 > 0 这里将其也写入到 buffer 中
        if (count > 0)
        {
            bufferSize = WriteRleValueToBuffer(_compressBuffer, bufferSize, value, count);
        }

        // 压缩数据
        using var memoryStream = new MemoryStream(buffer);
        using (var deflateStream = new DeflateStream(memoryStream, CompressionLevel.Fastest, true))
        {
            deflateStream.Write(_compressBuffer, 0, bufferSize);
        }

        // 返回压缩之后的大小
        return (int)memoryStream.Position;
    }

    /// <summary>
    ///     解压数据块
    /// </summary>
    /// <param name="chunk"> 解压结果 </param>
    /// <param name="buffer"> 原始数据缓冲区 </param>
    /// <param name="size"> 缓冲区大小 </param>
    /// <exception cref="InvalidOperationException"> 缓冲区大小超出最大限制会抛出该异常 </exception>
    private void DecompressChunkData(TerrainChunk chunk, byte[] buffer, int size)
    {
        // 解压 Chunk 数据块中的数据
        using var deflateStream = new DeflateStream(new MemoryStream(buffer, 0, size), CompressionMode.Decompress);

        // Stream.Read 不保证一次填满目标缓冲区，必须持续读取到流末尾。
        var decompressSize = ReadToEnd(deflateStream, _compressBuffer);

        ValidateChunkData(_compressBuffer, decompressSize);

        var bufferIndex = 0;
        // 从 buffer 中获取 Shaft （温度和湿度）信息并填入 TerrainChunk 对象中
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var shaftValueByte = _compressBuffer[bufferIndex++];
                var shaftValue = Terrain.ReplaceTemperature(Terrain.ReplaceHumidity(0, shaftValueByte & 0xF),
                    shaftValueByte >> 4);
                chunk.SetShaftValueFast(i, j, shaftValue);
            }
        }

        // Chunk 数据块的坐标系
        //       Y
        //       |
        //       |-256 区块高度
        //       |
        //       |
        //       |
        //       |
        //       |
        //       |
        //       |
        //       |          16 Shaft 轴的 x 索引
        //       |          |
        //       +------------------> X
        //      /
        //     /
        //    /
        //   /-16  Shaft 的 y 索引
        //  /
        // Z
        var cellX = 0;
        var cellY = 0;
        var cellZ = 0;
        while (bufferIndex < decompressSize)
        {
            // 读取经过 RLE 压缩的 Cell 数据
            bufferIndex = ReadRleValueFromBuffer(_compressBuffer, bufferIndex, decompressSize, out var cellValue,
                out var count);
            // 将获取到的 ValueCountPair 逐层 (16 x 16 x 256) 的填充到 TerrainChunk 的 Cells 中
            for (var k = 0; k < count; k++)
            {
                chunk.SetCellValueFast(cellX, cellY, cellZ, cellValue);
                cellX++;
                if (cellX < 16)
                {
                    continue;
                }

                cellX = 0;
                cellZ++;
                if (cellZ < 16)
                {
                    continue;
                }

                cellZ = 0;
                cellY++;
            }
        }
    }

    internal static int ReadToEnd(Stream stream, byte[] buffer)
    {
        var totalBytesRead = 0;
        while (totalBytesRead < buffer.Length)
        {
            var bytesRead = stream.Read(buffer, totalBytesRead, buffer.Length - totalBytesRead);
            if (bytesRead == 0)
            {
                return totalBytesRead;
            }

            totalBytesRead += bytesRead;
        }

        return stream.ReadByte() >= 0
            ? throw new InvalidOperationException("Deflate buffer overflow.")
            : totalBytesRead;
    }

    private static void ValidateChunkData(byte[] buffer, int size)
    {
        const int shaftDataSize = 16 * 16;
        const int cellsCount = 16 * 16 * 256;
        if (size < shaftDataSize)
        {
            throw new InvalidOperationException("Corrupt chunk data: shaft data is truncated.");
        }

        var bufferIndex = shaftDataSize;
        var decodedCellsCount = 0;
        while (bufferIndex < size)
        {
            bufferIndex = ReadRleValueFromBuffer(buffer, bufferIndex, size, out _, out var count);
            if (count > cellsCount - decodedCellsCount)
            {
                throw new InvalidOperationException("Corrupt chunk data: cell count exceeds chunk capacity.");
            }

            decodedCellsCount += count;
        }

        if (decodedCellsCount != cellsCount)
        {
            throw new InvalidOperationException("Corrupt chunk data.");
        }
    }

    /// <summary>
    ///     从缓冲区读取一个 int 值 4 Byte
    /// </summary>
    /// <param name="buffer"> 读取的缓冲区 </param>
    /// <param name="i"> 缓冲区索引 </param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadIntFromBuffer(byte[] buffer, int i)
    {
        return buffer[i] + (buffer[i + 1] << 8) + (buffer[i + 2] << 16) + (buffer[i + 3] << 24);
    }

    /// <summary>
    ///     从 buffer 中读取经过 Run-Length Encoding (RLE) 游程编码压缩的数据
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         RLE: 游程编码压缩是一种对于有大量重复数据的场景而行之有效的压缩算法，核心思想是将连续重复的值（称为游程）替换为一个值和重复次数。
    ///         简单的示例: AAAABBBCCCCCDD -> A4B3C5D2
    ///     </para>
    ///     <para>
    ///         ValueCountPair 结构: 前 2 个 Byte [ Count (6 bits) | Value (10 bits) ]，后 2 个 Byte 空闲，如果 Count = 15, 则在该
    ///         ValueCountPair 4 Byte 之后的 1 个 Byte 和 Count 的加和表示该值重复的次数。
    ///     </para>
    /// </remarks>
    /// <param name="buffer"> 数据源 </param>
    /// <param name="i"> 开始读取 buffer 的索引 </param>
    /// <param name="size"> buffer 中有效数据的长度 </param>
    /// <param name="value"> 读取到的值 </param>
    /// <param name="count"> 该值重复的次数 </param>
    /// <returns> 下一个读取点的索引位置 </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadRleValueFromBuffer(byte[] buffer, int i, int size, out int value, out int count)
    {
        if (i > size - sizeof(int))
        {
            throw new InvalidOperationException("Corrupt chunk data: RLE value is truncated.");
        }

        var valueCountPair = ReadIntFromBuffer(buffer, i);

        // 注意: 在存档文件中，光照信息被清除并被用于存储 RLE 算法中 Cell 的重复次数
        count = Terrain.ExtractLight(valueCountPair);
        value = Terrain.ReplaceLight(valueCountPair, 0);
        if (count < 15)
        {
            count += 1;
            return i + 4;
        }

        if (i + 4 >= size)
        {
            throw new InvalidOperationException("Corrupt chunk data: RLE count is truncated.");
        }

        count = buffer[i + 4] + 16;
        return i + 5;
    }

    /// <summary>
    ///     写入一个 int 值 4 Byte 到缓冲区
    /// </summary>
    /// <param name="buffer"> 写入的目标缓冲区 </param>
    /// <param name="i"> 缓冲区索引 </param>
    /// <param name="data"> 写入的数据 </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteIntToBuffer(byte[] buffer, int i, int data)
    {
        buffer[i] = (byte)data;
        buffer[i + 1] = (byte)(data >> 8);
        buffer[i + 2] = (byte)(data >> 16);
        buffer[i + 3] = (byte)(data >> 24);
    }

    /// <summary>
    ///     使用 RLE 算法将数据写入到缓冲区
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         RLE: 游程编码压缩是一种对于有大量重复数据的场景而行之有效的压缩算法，核心思想是将连续重复的值（称为游程）替换为一个值和重复次数。
    ///         简单的示例: AAAABBBCCCCCDD -> A4B3C5D2
    ///     </para>
    ///     <para>
    ///         ValueCountPair 结构: 前 2 个 Byte [ Count (6 bits) | Value (10 bits) ]，后 2 个 Byte 空闲，如果 Count > 15, 则在该
    ///         ValueCountPair 4 Byte 之后的 1 个 Byte 和 Count 的加和表示该值重复的次数。
    ///     </para>
    /// </remarks>
    /// <param name="buffer"> 写入的 buffer </param>
    /// <param name="i"> 写入 buffer 索引 </param>
    /// <param name="value"> 写入的值 </param>
    /// <param name="count"> 重复的次数 </param>
    /// <returns> 返回下一次写入的索引 </returns>
    /// <exception cref="InvalidOperationException"> 重复次数超出最大次数或者 Value 值超出最大值会报告该错误 </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteRleValueToBuffer(byte[] buffer, int i, int value, int count)
    {
        // 如果重复次数 < 16, 直接组装 ValueCountPair 然后写入 buffer
        int valueCountPair;
        if (count < 16)
        {
            // 注意: 在存档文件中，光照信息被清除并被用于存储 RLE 算法中 Cell 的重复次数
            valueCountPair = Terrain.ReplaceLight(value, count - 1);
            WriteIntToBuffer(buffer, i, valueCountPair);
            return i + 4;
        }

        // 如果重复次数 > 16 而 <= 271 则在 ValueCountPair(4Byte) 之后加一个字节存储减去 16 之后的重复次数
        if (count <= 271)
        {
            // 注意: 在存档文件中，光照信息被清除并被用于存储 RLE 算法中 Cell 的重复次数
            valueCountPair = Terrain.ReplaceLight(value, 15);
            WriteIntToBuffer(buffer, i, valueCountPair);
            buffer[i + 4] = (byte)(count - 16);
            return i + 5;
        }

        throw new InvalidOperationException("Count too large.");
    }

    public interface IStorage : IDisposable
    {
        /// <summary>
        ///     打开 Region 文件夹，并将需要替换或更新的 Region 文件进行处理
        /// </summary>
        /// <param name="directoryName"> Region 文件目录 </param>
        /// <param name="suffix"> Region 目录的后缀，默认为空字符串 </param>
        void Open(string directoryName, string suffix = "");

        /// <summary>
        ///     从 Region 文件中加载一个 Chunk 数据块
        /// </summary>
        /// <param name="coords"> 坐标 </param>
        /// <param name="buffer"> Chunk 数据块缓冲区 </param>
        /// <returns> 读取到的字节数, -1 代表加载错误 </returns>
        int Load(Point2 coords, byte[] buffer);

        /// <summary>
        ///     保存一个 Chunk 数据块到 Region 文件，如果文件不存在，则创建，存在则更新
        /// </summary>
        /// <param name="coords"> 坐标 </param>
        /// <param name="buffer"> Chunk 数据块缓冲区 </param>
        /// <param name="size"> 缓冲区大小 </param>
        void Save(Point2 coords, byte[] buffer, int size);

        /// <summary>
        ///     判断数据块或 Region 文件是否存在
        /// </summary>
        /// <param name="coords"> 坐标 </param>
        /// <returns> 是否存在 </returns>
        bool Exists(Point2 coords);
    }

    public class RegionFileStorage(TerrainSerializer24 terrainSerializer24) : IStorage
    {
        private const int _maxOpenedStreams = 100;

        private const int _extraSpaceBytes = 1024;

        private const int _regionDataOffset = 2052;

        private static readonly uint _regionMagic = MakeFourCc("RGN1");

        private static readonly uint _regionChunkMagic = MakeFourCc("CHK1");

        private readonly Lock _lock = new();

        private readonly Queue<Stream> _openedStreams = new();

        private readonly Dictionary<Point2, Stream?> _streamsByRegion = new();

        private string _regionsDirectoryName = string.Empty;

        private TerrainSerializer24 _terrainSerializer = terrainSerializer24;

        private string _tmpFilePath = string.Empty;

        public void Dispose()
        {
            while (_openedStreams.Count > 0)
            {
                _openedStreams.Dequeue().Dispose();
            }
        }

        /// <summary>
        ///     打开 Region 文件夹，并将需要替换或更新的 Region 文件进行处理
        /// </summary>
        /// <param name="directoryName"> Region 文件目录 </param>
        /// <param name="suffix"> Region 目录的后缀，默认为空字符串 </param>
        public void Open(string directoryName, string suffix = "")
        {
            _regionsDirectoryName = Storage.CombinePaths(directoryName, "Regions" + suffix);
            Storage.CreateDirectory(_regionsDirectoryName);
            _tmpFilePath = Storage.CombinePaths(_regionsDirectoryName, "tmp");
            Storage.DeleteFile(_tmpFilePath);
            foreach (var item in Storage.ListFileNames(_regionsDirectoryName))
            {
                if (Storage.GetExtension(item) != ".new")
                {
                    continue;
                }

                var text = Storage.CombinePaths(_regionsDirectoryName, item);
                var text2 = Storage.ChangeExtension(text, "");
                if (!Storage.FileExists(text2))
                {
                    Storage.MoveFile(text, text2);
                }
                else
                {
                    Storage.DeleteFile(text);
                }
            }
        }

        /// <summary>
        ///     从 Region 文件中加载一个 Chunk 数据块
        /// </summary>
        /// <param name="coords"> 坐标 </param>
        /// <param name="buffer"> Chunk 数据块缓冲区 </param>
        /// <returns> 读取到的字节数, -1 代表加载错误 </returns>
        public int Load(Point2 coords, byte[] buffer)
        {
            lock (_lock)
            {
                var region = new Point2(coords.X >> 4, coords.Y >> 4);
                var chunkPosition = new Point2(coords.X & 0xF, coords.Y & 0xF);
                var regionStream = GetRegionStream(region, false);
                if (regionStream == null)
                {
                    return -1;
                }

                using var reader = new BinaryReader(regionStream, Encoding.UTF8, true);
                var directoryEntry = ReadDirectoryEntry(reader, chunkPosition);

                if (directoryEntry.Offset <= 0)
                {
                    return -1;
                }

                ReadData(reader, directoryEntry.Offset, buffer, directoryEntry.Size);
                return directoryEntry.Size;
            }
        }

        /// <summary>
        ///     保存一个 Chunk 数据块到 Region 文件，如果文件不存在，则创建，存在则更新
        /// </summary>
        /// <param name="coords"> 坐标 </param>
        /// <param name="buffer"> Chunk 数据块缓冲区 </param>
        /// <param name="size"> 缓冲区大小 </param>
        public void Save(Point2 coords, byte[] buffer, int size)
        {
            var region = new Point2(coords.X >> 4, coords.Y >> 4);
            var chunkPosition = new Point2(coords.X & 0xF, coords.Y & 0xF);
            var regionStream = GetRegionStream(region, true)!;
            string? newRegionFileName = null;

            using (var reader = new BinaryReader(regionStream, Encoding.UTF8, true))
            using (var writer = new BinaryWriter(regionStream, Encoding.UTF8, true))
            {
                var chunkIndex = chunkPosition.X + 16 * chunkPosition.Y;
                var entries = ReadDirectoryEntries(reader);
                var directoryEntry = entries[chunkIndex];

                DirectoryEntry entry;
                // 如果 Offset > 0 说明已存在的 Chunk, 准备更新 Region 文件中的 Chunk 数据块
                if (directoryEntry.Offset > 0)
                {
                    // 查找下一个 Chunk 数据块用于计算当前更新数据块的真实大小 realChunkSize
                    var nextChunkIndex = FindNextEntryIndex(entries, chunkIndex);
                    // 下一个数据块的索引如果 >= 0 说明当前数据块不是最后一个数据块, 准备更新当前数据块，
                    if (nextChunkIndex >= 0)
                    {
                        // 计算数据块的真实大小，下一个数据块 - 当前数据块 - Chunk魔数标识(4 Byte)
                        var realChunkSize = entries[nextChunkIndex].Offset - directoryEntry.Offset - 4;
                        // 如果新的数据的大小小于当前数据块的真实大小，直接覆盖原数据，并调整当前 Chunk 数据块的大小 Size
                        if (size <= realChunkSize)
                        {
                            WriteData(writer, directoryEntry.Offset, buffer, size);
                            entry = new DirectoryEntry
                            {
                                Offset = directoryEntry.Offset,
                                Size = size
                            };
                            WriteDirectoryEntry(writer, chunkPosition, entry);
                            regionStream.Flush();
                        }
                        // 如果更新数据块的尺寸大于当前数据块的真实大小，则创建新的 Region 文件并在处理完更新和复制
                        // 旧 Region 文件中的 Chunk 数据块之后，替换旧的 Region 文件
                        else
                        {
                            // 新的 Region 文件名
                            newRegionFileName = GetRegionPath(region);
                            using var stream = Storage.OpenFile(_tmpFilePath, OpenFileMode.Create);
                            using var binaryWriter = new BinaryWriter(stream);

                            var newEntries = new DirectoryEntry[entries.Length];

                            // Region 文件 Chunk 数据块默认的最小偏移
                            var localRegionDataOffset = _regionDataOffset;
                            for (var i = 0; i < entries.Length; i++)
                            // 索引 i 等于当前 chunkIndex，更新当前的 Chunk 数据块映射条目的偏移和大小
                            {
                                if (i == chunkIndex)
                                {
                                    newEntries[i].Offset = localRegionDataOffset;
                                    newEntries[i].Size = size;
                                    // 增加一定的余量的 Chunk 数据块的真实大小，以避免频繁的重建 Region 文件
                                    localRegionDataOffset += CalculateIdealEntrySpace(newEntries[i].Size);
                                }
                                // 否则，将偏移 > 0 的数据块 Chunk 数据块映射条目的偏移和大小修正
                                else if (entries[i].Offset > 0)
                                {
                                    newEntries[i].Offset = localRegionDataOffset;
                                    newEntries[i].Size = entries[i].Size;
                                    // 增加一定的余量的 Chunk 数据块的真实大小，以避免频繁的重建 Region 文件
                                    localRegionDataOffset += CalculateIdealEntrySpace(newEntries[i].Size);
                                }
                            }

                            ResizeStream(stream, localRegionDataOffset);
                            binaryWriter.Write(_regionMagic);

                            // 写入 Chunk 数据块映射表
                            WriteDirectoryEntries(binaryWriter, newEntries);

                            var buffer2 = new byte[entries.Max(e => e.Size)];
                            for (var j = 0; j < entries.Length; j++)
                            // 索引 i 等于当前 chunkIndex，根据 Chunk 数据块映射条目的偏移和大小更新数据块
                            {
                                if (j == chunkIndex)
                                {
                                    WriteData(binaryWriter, newEntries[j].Offset, buffer, size);
                                }
                                // 否则，将偏移 > 0 的数据块 Chunk 数据块根据映射条目的偏移和大小复制到新的 Region 文件
                                else if (entries[j].Offset > 0)
                                {
                                    ReadData(reader, entries[j].Offset, buffer2, entries[j].Size);
                                    WriteData(binaryWriter, newEntries[j].Offset, buffer2, newEntries[j].Size);
                                }
                            }
                        }
                    }
                    // 下一个数据 < 0，一般是 -1 代表是最后一个 Chunk 数据块，直接往 Region 文件的末尾写入数据，并更新数据块的 Size
                    // 同时，增加最后一个数据块的真实大小备用
                    else
                    {
                        if (directoryEntry.Offset + 4 + size > regionStream.Length)
                        {
                            ResizeStream(regionStream, directoryEntry.Offset + CalculateIdealEntrySpace(size));
                        }

                        WriteData(writer, directoryEntry.Offset, buffer, size);
                        entry = new DirectoryEntry
                        {
                            Offset = directoryEntry.Offset,
                            Size = size
                        };

                        // 写入单个 Chunk 数据块映射条目
                        WriteDirectoryEntry(writer, chunkPosition, entry);
                        regionStream.Flush();
                    }
                }
                // 如果 Offset <= 0 说明该 Chunk 数据块不存在，直接往 Region 文件的末尾写入新的 Region 数据块
                else
                {
                    var regionSize = (int)regionStream.Length;
                    ResizeStream(regionStream, regionSize + CalculateIdealEntrySpace(size));
                    WriteData(writer, regionSize, buffer, size);
                    entry = new DirectoryEntry
                    {
                        Offset = regionSize,
                        Size = size
                    };

                    // 写入单个 Chunk 数据块映射条目
                    WriteDirectoryEntry(writer, chunkPosition, entry);
                    regionStream.Flush();
                }
            }

            // 如果新的 Region 文件名称不为空，说明有新的 Region 文件生成，需要提替换旧的 Region 文件
            if (string.IsNullOrEmpty(newRegionFileName))
            {
                return;
            }

            regionStream.Dispose();
            var text2 = newRegionFileName + ".new";
            Storage.MoveFile(_tmpFilePath, text2);
            Storage.MoveFile(text2, newRegionFileName);
        }

        /// <summary>
        ///     判断数据块或 Region 文件是否存在
        /// </summary>
        /// <param name="coords"> 坐标 </param>
        /// <returns> 是否存在 </returns>
        public bool Exists(Point2 coords)
        {
            lock (_lock)
            {
                var exists = false;
                var region = new Point2(coords.X >> 4, coords.Y >> 4);
                var chunk = new Point2(coords.X & 0xF, coords.Y & 0xF);
                var regionStream = GetRegionStream(region, false);
                if (regionStream == null)
                {
                    return exists;
                }

                using var reader = new BinaryReader(regionStream, Encoding.UTF8, true);
                var directoryEntry = ReadDirectoryEntry(reader, chunk);
                if (directoryEntry.Offset > 0)
                {
                    exists = true;
                }

                return exists;
            }
        }

        private string GetRegionPath(Point2 region)
        {
            return string.Format("{0}/Region {1},{2}.dat", new object[3] { _regionsDirectoryName, region.X, region.Y });
        }

        private Stream? GetRegionStream(Point2 region, bool createNew)
        {
            if (_streamsByRegion.TryGetValue(region, out var value) && value != null && value.CanRead)
            {
                return value;
            }

            var regionPath = GetRegionPath(region);
            if (Storage.FileExists(regionPath))
            {
                value = Storage.OpenFile(regionPath, OpenFileMode.ReadWrite);
                using (var binaryReader = new BinaryReader(value, Encoding.UTF8, true))
                {
                    if (binaryReader.ReadUInt32() != _regionMagic)
                    {
                        throw new InvalidOperationException($"Invalid region file {region} magic.");
                    }
                }

                _openedStreams.Enqueue(value);
            }
            else if (createNew)
            {
                value = Storage.OpenFile(regionPath, OpenFileMode.Create);
                _openedStreams.Enqueue(value);
                using var binaryWriter = new BinaryWriter(value, Encoding.UTF8, true);
                binaryWriter.Write(_regionMagic);
                WriteDirectoryEntries(binaryWriter, new DirectoryEntry[256]);
            }
            else
            {
                value = null;
            }

            _streamsByRegion[region] = value;
            while (_openedStreams.Count > _maxOpenedStreams)
            {
                _openedStreams.Dequeue().Dispose();
            }

            return value;
        }

        private static void ReadData(BinaryReader reader, int offset, byte[] buffer, int size)
        {
            if (size > buffer.Length)
            {
                throw new InvalidOperationException("Region file chunk exceeds the read buffer capacity.");
            }

            reader.BaseStream.Position = offset;
            if (reader.ReadUInt32() != _regionChunkMagic)
            {
                throw new InvalidOperationException("Invalid region file chunk magic.");
            }

            try
            {
                reader.BaseStream.ReadExactly(buffer.AsSpan(0, size));
            }
            catch (EndOfStreamException e)
            {
                throw new InvalidOperationException("Region file is truncated.", e);
            }
        }

        private static DirectoryEntry ReadDirectoryEntry(BinaryReader reader)
        {
            var result = default(DirectoryEntry);
            result.Offset = reader.ReadInt32();
            result.Size = reader.ReadInt32();
            if (result.Size is < 0 or > 2097152)
            {
                throw new InvalidOperationException(
                    "Region file entry size out of bounds, likely corrupt region file.");
            }

            return result;
        }

        private static DirectoryEntry ReadDirectoryEntry(BinaryReader reader, Point2 chunk)
        {
            var num = chunk.X + 16 * chunk.Y;
            reader.BaseStream.Position = 4 + num * 8;
            return ReadDirectoryEntry(reader);
        }

        private static DirectoryEntry[] ReadDirectoryEntries(BinaryReader reader)
        {
            reader.BaseStream.Position = 4L;
            var array = new DirectoryEntry[256];
            for (var i = 0; i < 256; i++)
            {
                array[i] = ReadDirectoryEntry(reader);
            }

            return array;
        }

        private static void WriteData(BinaryWriter writer, int offset, byte[] buffer, int size)
        {
            writer.BaseStream.Position = offset;
            writer.Write(_regionChunkMagic);
            writer.BaseStream.Write(buffer, 0, size);
        }

        private static void WriteDirectoryEntry(BinaryWriter writer, DirectoryEntry entry)
        {
            writer.Write(entry.Offset);
            writer.Write(entry.Size);
        }

        private static void WriteDirectoryEntry(BinaryWriter writer, Point2 chunk, DirectoryEntry entry)
        {
            var num = chunk.X + 16 * chunk.Y;
            writer.BaseStream.Position = 4 + num * 8;
            WriteDirectoryEntry(writer, entry);
        }

        private static void WriteDirectoryEntries(BinaryWriter writer, DirectoryEntry[] entries)
        {
            writer.BaseStream.Position = 4L;
            for (var i = 0; i < 256; i++)
            {
                WriteDirectoryEntry(writer, entries[i]);
            }
        }

        private static void ResizeStream(Stream stream, int size)
        {
            if (size > 268435456)
            {
                throw new InvalidOperationException("Region file too large.");
            }

            stream.SetLength(size);
        }

        private static int FindNextEntryIndex(DirectoryEntry[] entries, int index)
        {
            var result = -1;
            var num = int.MaxValue;
            for (var i = 0; i < entries.Length; i++)
            {
                var num2 = entries[i].Offset - entries[index].Offset;
                if (num2 <= 0 || num2 >= num)
                {
                    continue;
                }

                num = num2;
                result = i;
            }

            return result;
        }

        /// <summary>
        ///     计算合适的 Chunk 空闲空间以避免频繁的重建 Region 文件
        /// </summary>
        /// <param name="size"> 占用的大小 </param>
        /// <returns> 调整后的大小 </returns>
        private static int CalculateIdealEntrySpace(int size)
        {
            return size + _extraSpaceBytes + 4;
        }

        /// <summary>
        ///     将长度为 4 的字符串转换成无符号整形数 unit
        /// </summary>
        /// <param name="s"> 需要转换的字符串 </param>
        /// <returns> 转换结果 </returns>
        private static uint MakeFourCc(string s)
        {
            if (s.Length != 4)
            {
                throw new InvalidExpressionException($"input string: {s} is not match");
            }

            return ((uint)s[3] << 24) | ((uint)s[2] << 16) | ((uint)s[1] << 8) | s[0];
        }

        private struct DirectoryEntry
        {
            public int Offset;

            public int Size;
        }
    }
}
