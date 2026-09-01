using System.Buffers;
using System.Buffers.Binary;

namespace Content.Packaging;

internal static class PngPayloadValidator
{
    private static readonly byte[] _signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private const int _bufferSize = 64 * 1024;

    public static void Validate(Stream stream, int expectedWidth, int expectedHeight, string path)
    {
        Span<byte> signature = stackalloc byte[8];
        ReadExactly(stream, signature, path);
        if (!signature.SequenceEqual(_signature))
        {
            throw new ContentPackageException($"Image payload '{path}' is not a PNG.");
        }

        var sawHeader = false;
        var sawImageData = false;
        var sawEnd = false;
        while (!sawEnd)
        {
            var chunkHeader = new byte[8];
            ReadExactly(stream, chunkHeader, path);
            var length = BinaryPrimitives.ReadUInt32BigEndian(chunkHeader[..4]);
            if (length > ContentPackageReader.MaxEntryBytes)
            {
                throw new ContentPackageException($"PNG chunk in '{path}' exceeds the package entry limit.");
            }

            var type = chunkHeader[4..8].ToArray();
            if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8))
            {
                throw new ContentPackageException($"PNG payload '{path}' cannot be animated.");
            }

            if (type[0] is >= (byte)'A' and <= (byte)'Z' &&
                !type.SequenceEqual("IHDR"u8) && !type.SequenceEqual("PLTE"u8) &&
                !type.SequenceEqual("IDAT"u8) && !type.SequenceEqual("IEND"u8))
            {
                throw new ContentPackageException($"PNG payload '{path}' contains an unsupported critical chunk.");
            }

            var crc = Crc32.Start();
            crc = Crc32.Append(crc, type);
            var data = ArrayPool<byte>.Shared.Rent(_bufferSize);
            try
            {
                var remaining = (long)length;
                var headerData = new byte[Math.Min((int)length, 13)];
                var offset = 0;
                while (remaining > 0)
                {
                    var requested = (int)Math.Min(data.Length, remaining);
                    var read = stream.Read(data, 0, requested);
                    if (read == 0)
                    {
                        throw new ContentPackageException($"PNG payload '{path}' is truncated.");
                    }

                    if (offset < headerData.Length)
                    {
                        var copyLength = Math.Min(read, headerData.Length - offset);
                        data.AsSpan(0, copyLength).CopyTo(headerData.AsSpan(offset));
                    }

                    offset += read;
                    remaining -= read;
                    crc = Crc32.Append(crc, data.AsSpan(0, read));
                }

                var expectedCrcBytes = new byte[4];
                ReadExactly(stream, expectedCrcBytes, path);
                if (BinaryPrimitives.ReadUInt32BigEndian(expectedCrcBytes) != Crc32.Finish(crc))
                {
                    throw new ContentPackageException($"PNG chunk CRC in '{path}' is invalid.");
                }

                if (type.SequenceEqual("IHDR"u8))
                {
                    if (sawHeader || length != 13)
                    {
                        throw new ContentPackageException($"PNG payload '{path}' has an invalid IHDR chunk.");
                    }

                    ValidateHeader(headerData, expectedWidth, expectedHeight, path);
                    sawHeader = true;
                }
                else if (type.SequenceEqual("IDAT"u8))
                {
                    if (!sawHeader)
                    {
                        throw new ContentPackageException($"PNG payload '{path}' has IDAT before IHDR.");
                    }

                    sawImageData = true;
                }
                else if (type.SequenceEqual("IEND"u8))
                {
                    if (!sawHeader || !sawImageData || length != 0)
                    {
                        throw new ContentPackageException($"PNG payload '{path}' has an invalid IEND chunk.");
                    }

                    sawEnd = true;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(data);
            }
        }

        if (stream.ReadByte() != -1)
        {
            throw new ContentPackageException($"PNG payload '{path}' contains data after IEND.");
        }
    }

    private static void ValidateHeader(ReadOnlySpan<byte> data, int expectedWidth, int expectedHeight, string path)
    {
        if (BinaryPrimitives.ReadInt32BigEndian(data[..4]) != expectedWidth ||
            BinaryPrimitives.ReadInt32BigEndian(data[4..8]) != expectedHeight ||
            data[10] != 0 || data[11] != 0 || data[12] != 0 || !IsValidColorDepth(data[8], data[9]))
        {
            throw new ContentPackageException(
                $"PNG payload '{path}' does not match its declared dimensions or format.");
        }
    }

    private static bool IsValidColorDepth(byte depth, byte colorType) => colorType switch
    {
        0 => depth is 1 or 2 or 4 or 8 or 16,
        2 => depth is 8 or 16,
        3 => depth is 1 or 2 or 4 or 8,
        4 or 6 => depth is 8 or 16,
        _ => false
    };

    private static void ReadExactly(Stream stream, Span<byte> destination, string path)
    {
        var offset = 0;
        while (offset < destination.Length)
        {
            var read = stream.Read(destination[offset..]);
            if (read == 0)
            {
                throw new ContentPackageException($"PNG payload '{path}' is truncated.");
            }

            offset += read;
        }
    }

    private static class Crc32
    {
        public static uint Start() => uint.MaxValue;

        public static uint Append(uint crc, ReadOnlySpan<byte> data)
        {
            foreach (var value in data)
            {
                crc ^= value;
                for (var bit = 0; bit < 8; bit++)
                {
                    crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
                }
            }

            return crc;
        }

        public static uint Finish(uint crc) => ~crc;
    }
}
