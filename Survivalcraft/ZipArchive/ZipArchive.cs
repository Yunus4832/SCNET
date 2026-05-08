using System.IO.Compression;
using System.Text;

namespace Game.ZipArchive;

public class ZipArchive : IDisposable
{
    public enum Compression : ushort
    {
        Store = 0,
        Deflate = 8
    }

    public static uint[] CrcTable;

    public byte[] CentralDirImage = [];

    public string Comment = "";

    public ushort ExistingFiles;

    public List<ZipArchiveEntry> Files = [];

    public bool ForceDeflating;

    public bool KeepStreamOpen;

    public bool ReadOnly;

    public required Stream ZipFileStream;

    static ZipArchive()
    {
        CrcTable = new uint[256];
        for (var i = 0; i < CrcTable.Length; i++)
        {
            var num = (uint)i;
            for (var j = 0; j < 8; j++)
            {
                num = (uint)((num & 1) == 0 ? (int)(num >> 1) : -306674912 ^ (int)(num >> 1));
            }

            CrcTable[i] = num;
        }
    }

    public void Dispose()
    {
        Close();
    }

    public static ZipArchive Create(Stream stream, bool keepStreamOpen = false)
    {
        return new ZipArchive
        {
            Comment = string.Empty,
            ZipFileStream = stream,
            ReadOnly = false,
            KeepStreamOpen = keepStreamOpen
        };
    }

    public static ZipArchive Open(Stream stream, bool keepStreamOpen = false)
    {
        var zipArchive = new ZipArchive
        {
            ZipFileStream = stream,
            ReadOnly = true,
            KeepStreamOpen = keepStreamOpen,
        };
        return zipArchive.ReadFileInfo() ? zipArchive : throw new InvalidDataException();
    }

    public void AddStream(string filenameInZip, Stream source)
    {
        if (ReadOnly)
        {
            throw new InvalidOperationException("Writing is not allowed");
        }

        var zipArchiveEntry = new ZipArchiveEntry
        {
            Method = Compression.Deflate,
            FilenameInZip = NormalizedFilename(filenameInZip),
            Comment = string.Empty,
            Crc32 = 0u,
            HeaderOffset = (uint)ZipFileStream.Position,
            ModifyTime = DateTime.Now
        };
        WriteLocalHeader(zipArchiveEntry);
        zipArchiveEntry.FileOffset = (uint)ZipFileStream.Position;
        Store(zipArchiveEntry, source);
        UpdateCrcAndSizes(zipArchiveEntry);
        Files.Add(zipArchiveEntry);
    }

    private void Close()
    {
        if (!ReadOnly)
        {
            var offset = (uint)ZipFileStream.Position;
            var num = 0u;
            ZipFileStream.Write(CentralDirImage, 0, CentralDirImage.Length);

            foreach (var file in Files)
            {
                var position = ZipFileStream.Position;
                WriteCentralDirRecord(file);
                num = (uint)((int)num + (int)(ZipFileStream.Position - position));
            }

            WriteEndRecord((uint)((int)num + CentralDirImage.Length), offset);
        }

        if (KeepStreamOpen)
        {
            return;
        }

        ZipFileStream.Flush();
        ZipFileStream.Dispose();
    }

    private static bool IsUTF8Bytes(byte[] data, int start, int count)
    {
        var charByteCounter = 1; //计算当前正分析的字符应还有的字节数
        var end = start + count;
        for (var i = start; i < end; i++)
        {
            var curByte = data[i]; //当前分析的字节.
            if (charByteCounter == 1)
            {
                if (curByte < 0x80)
                {
                    continue;
                }

                while (((curByte <<= 1) & 0x80) != 0)
                {
                    charByteCounter++;
                }

                if (charByteCounter is 1 or > 6)
                {
                    return false;
                }
            }
            else
            {
                if ((curByte & 0xC0) != 0x80)
                {
                    return false;
                }

                charByteCounter--;
            }
        }

        return charByteCounter > 1 ? throw new InvalidOperationException("非预期的byte格式") : true;
    }

    public List<ZipArchiveEntry> ReadCentralDir()
    {
        if (CentralDirImage == null)
        {
            throw new InvalidOperationException("Central directory currently does not exist");
        }

        var list = new List<ZipArchiveEntry>();
        ushort num;
        ushort num2;
        ushort num3;
        for (var i = 0;
             i < CentralDirImage.Length && BitConverter.ToUInt32(CentralDirImage, i) == 33639248;
             i += 46 + num + num2 + num3)
        {
            var method = BitConverter.ToUInt16(CentralDirImage, i + 10);
            var dt = BitConverter.ToUInt32(CentralDirImage, i + 12);
            var crc = BitConverter.ToUInt32(CentralDirImage, i + 16);
            var compressedSize = BitConverter.ToUInt32(CentralDirImage, i + 20);
            var fileSize = BitConverter.ToUInt32(CentralDirImage, i + 24);
            num = BitConverter.ToUInt16(CentralDirImage, i + 28);
            num2 = BitConverter.ToUInt16(CentralDirImage, i + 30);
            num3 = BitConverter.ToUInt16(CentralDirImage, i + 32);
            var headerOffset = BitConverter.ToUInt32(CentralDirImage, i + 42);
            var headerSize = (uint)(46 + num + num2 + num3);
            var zipArchiveEntry = new ZipArchiveEntry
            {
                Method = (Compression)method,
                FilenameInZip = NormalizedFilename(Encoding.UTF8.GetString(CentralDirImage, i + 46, num)),
                IsFilenameUtf8 = IsUTF8Bytes(CentralDirImage, i + 46, num),
                FileOffset = GetFileOffset(headerOffset),
                FileSize = fileSize,
                CompressedSize = compressedSize,
                HeaderOffset = headerOffset,
                HeaderSize = headerSize,
                Crc32 = crc,
                ModifyTime = DosTimeToDateTime(dt)
            };
            if (num3 > 0)
            {
                zipArchiveEntry.Comment = Encoding.UTF8.GetString(CentralDirImage, i + 46 + num + num2, num3);
            }

            list.Add(zipArchiveEntry);
        }

        return list;
    }

    public void ExtractFile(ZipArchiveEntry zfe, Stream stream)
    {
        if (!stream.CanWrite)
        {
            throw new InvalidOperationException("Stream cannot be written");
        }

        var array = new byte[4];
        ZipFileStream.Seek(zfe.HeaderOffset, SeekOrigin.Begin);
        ZipFileStream.ReadExactly(array, 0, 4);
        if (BitConverter.ToUInt32(array, 0) != 67324752)
        {
            throw new InvalidOperationException("Unsupported zip archive.");
        }

        Stream stream2;
        if (zfe.Method == Compression.Store)
        {
            stream2 = ZipFileStream;
        }
        else
        {
            if (zfe.Method != Compression.Deflate)
            {
                throw new InvalidOperationException("Unsupported zip archive.");
            }

            stream2 = new DeflateStream(ZipFileStream, CompressionMode.Decompress, true);
        }

        var array2 = new byte[16384];
        ZipFileStream.Seek(zfe.FileOffset, SeekOrigin.Begin);
        var num = zfe.FileSize;
        while (num != 0)
        {
            var num2 = stream2.Read(array2, 0, (int)Math.Min(num, array2.Length));
            stream.Write(array2, 0, num2);
            num = (uint)((int)num - num2);
        }

        stream.Flush();
        if (zfe.Method == Compression.Deflate)
        {
            stream2.Dispose();
        }
    }

    public uint GetFileOffset(uint headerOffset)
    {
        var array = new byte[2];
        ZipFileStream.Seek(headerOffset + 26, SeekOrigin.Begin);
        ZipFileStream.ReadExactly(array, 0, 2);
        var num = BitConverter.ToUInt16(array, 0);
        ZipFileStream.ReadExactly(array, 0, 2);
        var num2 = BitConverter.ToUInt16(array, 0);
        return (uint)(30 + num + num2 + headerOffset);
    }

    public void WriteLocalHeader(ZipArchiveEntry zfe)
    {
        var position = ZipFileStream.Position;
        var bytes = Encoding.UTF8.GetBytes(zfe.FilenameInZip);
        ZipFileStream.Write([80, 75, 3, 4, 20, 0], 0, 6);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)(zfe.EncodeUTF8 ? 2048 : 0)), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)zfe.Method), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes(DateTimeToDosTime(zfe.ModifyTime)), 0, 4);
        ZipFileStream.Write(new byte[12], 0, 12);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)bytes.Length), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2);
        ZipFileStream.Write(bytes, 0, bytes.Length);
        zfe.HeaderSize = (uint)(ZipFileStream.Position - position);
    }

    public void WriteCentralDirRecord(ZipArchiveEntry zfe)
    {
        var bytes = Encoding.UTF8.GetBytes(zfe.FilenameInZip);
        var bytes2 = Encoding.UTF8.GetBytes(zfe.Comment);
        ZipFileStream.Write([80, 75, 1, 2, 23, 11, 20, 0], 0, 8);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)(zfe.EncodeUTF8 ? 2048 : 0)), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)zfe.Method), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes(DateTimeToDosTime(zfe.ModifyTime)), 0, 4);
        ZipFileStream.Write(BitConverter.GetBytes(zfe.Crc32), 0, 4);
        ZipFileStream.Write(BitConverter.GetBytes(zfe.CompressedSize), 0, 4);
        ZipFileStream.Write(BitConverter.GetBytes(zfe.FileSize), 0, 4);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)bytes.Length), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)bytes2.Length), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)0), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)33024), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes(zfe.HeaderOffset), 0, 4);
        ZipFileStream.Write(bytes, 0, bytes.Length);
        ZipFileStream.Write(bytes2, 0, bytes2.Length);
    }

    public void WriteEndRecord(uint size, uint offset)
    {
        var bytes = Encoding.UTF8.GetBytes(Comment);
        ZipFileStream.Write([80, 75, 5, 6, 0, 0, 0, 0], 0, 8);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)Files.Count + ExistingFiles), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)Files.Count + ExistingFiles), 0, 2);
        ZipFileStream.Write(BitConverter.GetBytes(size), 0, 4);
        ZipFileStream.Write(BitConverter.GetBytes(offset), 0, 4);
        ZipFileStream.Write(BitConverter.GetBytes((ushort)bytes.Length), 0, 2);
        ZipFileStream.Write(bytes, 0, bytes.Length);
    }

    public void Store(ZipArchiveEntry zfe, Stream source)
    {
        var array = new byte[16384];
        var num = 0u;
        var position = ZipFileStream.Position;
        var position2 = source.Position;
        var stream = zfe.Method != 0
            ? new DeflateStream(ZipFileStream, CompressionMode.Compress, true)
            : ZipFileStream;
        zfe.Crc32 = 4294967295u;
        int num2;
        do
        {
            num2 = source.Read(array, 0, array.Length);
            num = (uint)((int)num + num2);
            if (num2 <= 0)
            {
                continue;
            }

            stream.Write(array, 0, num2);
            for (var num3 = 0u; num3 < num2; num3++)
            {
                zfe.Crc32 = CrcTable[(zfe.Crc32 ^ array[num3]) & 0xFF] ^ (zfe.Crc32 >> 8);
            }
        } while (num2 == array.Length);

        stream.Flush();
        if (zfe.Method == Compression.Deflate)
        {
            stream.Dispose();
        }

        zfe.Crc32 ^= 4294967295u;
        zfe.FileSize = num;
        zfe.CompressedSize = (uint)(ZipFileStream.Position - position);
        if (zfe.Method != Compression.Deflate ||
            ForceDeflating ||
            !source.CanSeek ||
            zfe.CompressedSize <= zfe.FileSize
           )
        {
            return;
        }

        zfe.Method = Compression.Store;
        ZipFileStream.Position = position;
        ZipFileStream.SetLength(position);
        source.Position = position2;
        Store(zfe, source);
    }

    public uint DateTimeToDosTime(DateTime dt)
    {
        return (uint)((dt.Second / 2) | (dt.Minute << 5) | (dt.Hour << 11) | (dt.Day << 16) | (dt.Month << 21) |
                      ((dt.Year - 1980) << 25));
    }

    public DateTime DosTimeToDateTime(uint dt)
    {
        return new DateTime((int)((dt >> 25) + 1980), (int)((dt >> 21) & 0xF), (int)((dt >> 16) & 0x1F),
            (int)((dt >> 11) & 0x1F), (int)((dt >> 5) & 0x3F), (int)((dt & 0x1F) * 2));
    }

    public void UpdateCrcAndSizes(ZipArchiveEntry zfe)
    {
        var position = ZipFileStream.Position;
        ZipFileStream.Position = zfe.HeaderOffset + 8;
        ZipFileStream.Write(BitConverter.GetBytes((ushort)zfe.Method), 0, 2);
        ZipFileStream.Position = zfe.HeaderOffset + 14;
        ZipFileStream.Write(BitConverter.GetBytes(zfe.Crc32), 0, 4);
        ZipFileStream.Write(BitConverter.GetBytes(zfe.CompressedSize), 0, 4);
        ZipFileStream.Write(BitConverter.GetBytes(zfe.FileSize), 0, 4);
        ZipFileStream.Position = position;
    }

    public string NormalizedFilename(string filename)
    {
        var text = filename.Replace('\\', '/');
        var num = text.IndexOf(':');
        if (num >= 0)
        {
            text = text.Remove(0, num + 1);
        }

        return text.Trim('/');
    }

    public bool ReadFileInfo()
    {
        if (ZipFileStream.Length < 22)
        {
            return false;
        }

        try
        {
            ZipFileStream.Seek(-17L, SeekOrigin.End);
            var binaryReader = new BinaryReader(ZipFileStream);
            do
            {
                ZipFileStream.Seek(-5L, SeekOrigin.Current);
                if (binaryReader.ReadUInt32() != 101010256)
                {
                    continue;
                }

                ZipFileStream.Seek(6L, SeekOrigin.Current);
                var existingFiles = binaryReader.ReadUInt16();
                var num = binaryReader.ReadInt32();
                var num2 = binaryReader.ReadUInt32();
                var num3 = binaryReader.ReadUInt16();
                if (ZipFileStream.Position + num3 != ZipFileStream.Length)
                {
                    return false;
                }

                ExistingFiles = existingFiles;
                CentralDirImage = new byte[num];
                ZipFileStream.Seek(num2, SeekOrigin.Begin);
                ZipFileStream.ReadExactly(CentralDirImage, 0, num);
                ZipFileStream.Seek(num2, SeekOrigin.Begin);
                return true;
            } while (ZipFileStream.Position > 0);
        }
        catch
        {
            // ignored
        }

        return false;
    }
}
