using System.Data;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace Game.TerrainSerializers;

/// <summary>在区域文件中读写地形区块。</summary>
public class TerrainChunkSerializer : IDisposable
{
    private const int _rleCountShift = 10;

    private const int _rleCountMask = 15 << _rleCountShift;

    /// <summary>区块压缩、解压和区域存储共用的单块缓冲区容量，单位为字节。</summary>
    private const int _worstCaseChunkDataSize = 524_800;

    private readonly byte[] _loadCompressBuffer = new byte[_worstCaseChunkDataSize];

    private readonly Lock _loadLock = new();

    private readonly byte[] _loadStorageBuffer = new byte[_worstCaseChunkDataSize];

    private readonly byte[] _saveCompressBuffer = new byte[_worstCaseChunkDataSize];

    private readonly Lock _saveLock = new();

    private readonly byte[] _saveStorageBuffer = new byte[_worstCaseChunkDataSize];

    protected IStorage storage;

    public TerrainChunkSerializer(string directoryName, string suffix = "")
    {
        storage = new RegionFileStorage();
        storage.Open(directoryName, suffix);
    }

    public TerrainChunkSerializer()
    {
        storage = new RegionFileStorage();
    }

    public virtual void Dispose()
    {
        storage?.Dispose();
        storage = null!;
    }

    /// <summary>
    ///     保存内容就绪的已修改区块，成功后清除脏标记；失败向调用方传播并保留重试状态。
    /// </summary>
    public void SaveChunk(TerrainChunk chunk)
    {
        if (!TerrainSaveCoordinator.RequiresSave(chunk))
        {
            return;
        }

        SaveSnapshot(chunk.Coords, chunk.Cells, chunk.Shafts);
        chunk.ModificationCounter = 0;
    }

    /// <summary>仅在区域文件或其中的区块记录缺失时返回 false。</summary>
    public bool LoadChunk(TerrainChunk chunk)
    {
        lock (_loadLock)
        {
            try
            {
                var size = storage.Load(chunk.Coords, _loadStorageBuffer);
                if (size == -1) { return false; }

                if (size <= 0 || size > _loadStorageBuffer.Length)
                {
                    throw new InvalidDataException("Invalid stored chunk size.");
                }

                DecompressChunkData(chunk, _loadStorageBuffer, size);
                return true;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException
                                              or UnauthorizedAccessException)
            {
                var source = storage is RegionFileStorage region
                    ? region.GetChunkPath(chunk.Coords)
                    : storage.GetType().Name;
                throw new InvalidDataException(
                    $"Could not load chunk ({chunk.Coords.X},{chunk.Coords.Y}) from '{source}'.",
                    error
                );
            }
        }
    }

    /// <summary>保存已捕获的完整区块内容。</summary>
    public void SaveSnapshot(Point2 coords, int[] cells, long[] shafts)
    {
        lock (_saveLock)
        {
            var size = CompressChunkData(cells, shafts, _saveStorageBuffer);
            storage.Save(coords, _saveStorageBuffer, size);
        }
    }

    private int CompressChunkData(int[] cells, long[] shafts, byte[] buffer)
    {
        var bufferSize = 0;
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var shaftValue = shafts[i + j * 16];
                // 将温度和湿度写入到 buffer
                _saveCompressBuffer[bufferSize++] = (byte)((Terrain.ExtractTemperature(shaftValue) << 4) |
                                                           Terrain.ExtractHumidity(shaftValue));
            }
        }

        // 缓冲区大小不能大于一个 Chunk 数据块的最大大小
        if (bufferSize >= _saveCompressBuffer.Length)
        {
            throw new InvalidOperationException("Compression buffer overflow.");
        }

        // 首先使用 RLE 算法压缩重复的数据, 区块高度为 256
        var value = -1;
        var count = 0;
        for (var k = 0; k < 256; k++)
        {
            for (var l = 0; l < 16; l++)
            {
                for (var m = 0; m < 16; m++)
                {
                    // 获取下一个值, 注意: 重复计数字段使用格值的保留位10至13，内容与数据位保持原编码
                    var nextValue = Terrain.NormalizeCellValue(cells[TerrainChunk.CalculateCellIndex(m, k, l)]);
                    if (count == 0)
                    {
                        value = nextValue;
                        count = 1;
                        continue;
                    }

                    if (nextValue != value)
                    {
                        bufferSize = WriteRleValueToBuffer(_saveCompressBuffer, bufferSize, value, count);
                        value = nextValue;
                        count = 1;
                        continue;
                    }

                    count++;
                    if (count != 271)
                    {
                        continue;
                    }

                    bufferSize = WriteRleValueToBuffer(_saveCompressBuffer, bufferSize, value, count);
                    count = 0;
                }
            }
        }

        // 刷出尚未写入的最后一个游程。
        if (count > 0)
        {
            bufferSize = WriteRleValueToBuffer(_saveCompressBuffer, bufferSize, value, count);
        }

        using var memoryStream = new MemoryStream(buffer);
        using (var deflateStream = new DeflateStream(memoryStream, CompressionLevel.Fastest, true))
        {
            deflateStream.Write(_saveCompressBuffer, 0, bufferSize);
        }

        return (int)memoryStream.Position;
    }

    /// <summary>解压并校验完整区块数据，再将温湿度和单元格写入目标区块。</summary>
    private void DecompressChunkData(TerrainChunk chunk, byte[] buffer, int size)
    {
        using var deflateStream = new DeflateStream(new MemoryStream(buffer, 0, size), CompressionMode.Decompress);

        // Stream.Read 不保证一次填满目标缓冲区，必须持续读取到流末尾。
        var decompressSize = ReadToEnd(deflateStream, _loadCompressBuffer);

        ValidateChunkData(_loadCompressBuffer, decompressSize);

        var bufferIndex = 0;
        // 从 buffer 中获取 Shaft （温度和湿度）信息并填入 TerrainChunk 对象中
        for (var i = 0; i < 16; i++)
        {
            for (var j = 0; j < 16; j++)
            {
                var shaftValueByte = _loadCompressBuffer[bufferIndex++];
                var shaftValue = Terrain.ReplaceTemperature(
                    Terrain.ReplaceHumidity(0, shaftValueByte & 0xF),
                    shaftValueByte >> 4
                );
                chunk.SetShaftValueFast(i, j, shaftValue);
            }
        }

        // 存档中 X 变化最快，其次是 Z，最后是 Y。
        var cellX = 0;
        var cellY = 0;
        var cellZ = 0;
        while (bufferIndex < decompressSize)
        {
            bufferIndex = ReadRleValueFromBuffer(
                _loadCompressBuffer,
                bufferIndex,
                decompressSize,
                out var cellValue,
                out var count
            );
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

    /// <summary>从指定字节偏移读取小端 32 位整数。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadIntFromBuffer(byte[] buffer, int i)
    {
        return buffer[i] + (buffer[i + 1] << 8) + (buffer[i + 2] << 16) + (buffer[i + 3] << 24);
    }

    /// <summary>读取一个游程并返回下一条记录的字节偏移。</summary>
    /// <remarks>四字节小端格值的第 10 至 13 位保存计数码。码值 0 至 14 表示重复 1 至 15 次；码值 15 时再读一个字节，重复次数为该字节加 16。</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadRleValueFromBuffer(byte[] buffer, int i, int size, out int value, out int count)
    {
        if (i > size - sizeof(int))
        {
            throw new InvalidOperationException("Corrupt chunk data: RLE value is truncated.");
        }

        var valueCountPair = ReadIntFromBuffer(buffer, i);

        // 注意: 重复计数字段使用格值的保留位10至13，内容与数据位保持原编码
        count = (valueCountPair & _rleCountMask) >> _rleCountShift;
        value = Terrain.NormalizeCellValue(valueCountPair);
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

    /// <summary>在指定字节偏移写入小端 32 位整数。</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteIntToBuffer(byte[] buffer, int i, int data)
    {
        buffer[i] = (byte)data;
        buffer[i + 1] = (byte)(data >> 8);
        buffer[i + 2] = (byte)(data >> 16);
        buffer[i + 3] = (byte)(data >> 24);
    }

    /// <summary>将 1 至 271 个相同格值编码为一个游程，返回下一条记录的字节偏移。</summary>
    /// <remarks>前 15 种长度占四字节，长度达到 16 时使用第五个字节保存超出 16 的次数。</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteRleValueToBuffer(byte[] buffer, int i, int value, int count)
    {
        // 如果重复次数 < 16, 直接组装 ValueCountPair 然后写入 buffer
        int valueCountPair;
        if (count < 16)
        {
            // 注意: 重复计数字段使用格值的保留位10至13，内容与数据位保持原编码
            valueCountPair = Terrain.NormalizeCellValue(value) | (((count - 1) & 15) << _rleCountShift);
            WriteIntToBuffer(buffer, i, valueCountPair);
            return i + 4;
        }

        if (count <= 271)
        {
            // 注意: 重复计数字段使用格值的保留位10至13，内容与数据位保持原编码
            valueCountPair = Terrain.NormalizeCellValue(value) | _rleCountMask;
            WriteIntToBuffer(buffer, i, valueCountPair);
            buffer[i + 4] = (byte)(count - 16);
            return i + 5;
        }

        throw new InvalidOperationException("Count too large.");
    }

    public interface IStorage : IDisposable
    {
        /// <summary>打开用于存放区域文件的目录。</summary>
        void Open(string directoryName, string suffix = "");

        /// <summary>将区块记录读入缓冲区，返回读取的字节数；记录缺失时返回 -1。</summary>
        int Load(Point2 coords, byte[] buffer);

        /// <summary>将缓冲区的前 <paramref name="size"/> 个字节保存为指定区块的记录。</summary>
        void Save(Point2 coords, byte[] buffer, int size);

        /// <summary>判断区域文件中是否有指定区块的记录。</summary>
        bool Exists(Point2 coords);
    }

    public class RegionFileStorage : IStorage
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

        private string _tmpFilePath = string.Empty;

        public void Dispose()
        {
            while (_openedStreams.Count > 0)
            {
                _openedStreams.Dequeue().Dispose();
            }
        }

        /// <summary>打开区域目录并处理未完成的文件替换；原文件缺失时恢复 .new 文件。</summary>
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

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void Save(Point2 coords, byte[] buffer, int size)
        {
            lock (_lock)
            {
                SaveCore(coords, buffer, size);
            }
        }

        private void SaveCore(Point2 coords, byte[] buffer, int size)
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

        /// <inheritdoc />
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

        internal string GetChunkPath(Point2 coords) => GetRegionPath(new Point2(coords.X >> 4, coords.Y >> 4));

        private string GetRegionPath(Point2 region)
        {
            return string.Format("{0}/Region {1},{2}.dat", new object[3] { _regionsDirectoryName, region.X, region.Y });
        }

        private Stream? GetRegionStream(Point2 region, bool createNew)
        {
            if (_streamsByRegion.TryGetValue(region, out var value) && value is { CanRead: true })
            {
                return value;
            }

            var regionPath = GetRegionPath(region);
            try { value = Storage.OpenFile(regionPath, OpenFileMode.ReadWrite); }
            catch (FileNotFoundException)
            {
                if (!createNew) { return null; }

                value = Storage.OpenFile(regionPath, OpenFileMode.Create);
                try
                {
                    using var writer = new BinaryWriter(value, Encoding.UTF8, true);
                    writer.Write(_regionMagic);
                    WriteDirectoryEntries(writer, new DirectoryEntry[256]);
                }
                catch
                {
                    value.Dispose();
                    throw;
                }
            }

            try
            {
                value.Position = 0;
                using var reader = new BinaryReader(value, Encoding.UTF8, true);
                if (value.Length < _regionDataOffset || reader.ReadUInt32() != _regionMagic)
                {
                    throw new InvalidDataException($"Invalid region header in '{regionPath}'.");
                }
            }
            catch
            {
                value.Dispose();
                throw;
            }

            _openedStreams.Enqueue(value);
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
            if (result.Offset == 0 && result.Size == 0) { return result; }

            if (result.Offset < _regionDataOffset ||
                result.Size <= 0 ||
                result.Size > 2097152 ||
                (long)result.Offset + sizeof(uint) + result.Size > reader.BaseStream.Length)
            {
                throw new InvalidOperationException(
                    "Region file entry size out of bounds, likely corrupt region file."
                );
            }

            return result;
        }

        private static DirectoryEntry ReadDirectoryEntry(BinaryReader reader, Point2 chunk)
        {
            var entryIndex = chunk.X + 16 * chunk.Y;
            reader.BaseStream.Position = 4 + entryIndex * 8;
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
            var entryIndex = chunk.X + 16 * chunk.Y;
            writer.BaseStream.Position = 4 + entryIndex * 8;
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
            var smallestOffsetDelta = int.MaxValue;
            for (var i = 0; i < entries.Length; i++)
            {
                var offsetDelta = entries[i].Offset - entries[index].Offset;
                if (offsetDelta <= 0 || offsetDelta >= smallestOffsetDelta)
                {
                    continue;
                }

                smallestOffsetDelta = offsetDelta;
                result = i;
            }

            return result;
        }

        /// <summary>为区块数据预留增长空间，减少区域文件的重建次数。</summary>
        private static int CalculateIdealEntrySpace(int size)
        {
            return size + _extraSpaceBytes + 4;
        }

        /// <summary>将四字符标识按小端顺序打包为 32 位文件签名。</summary>
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
