using System.Buffers.Binary;

using Engine.Core.Mathematics;

namespace Engine.Media.Images.Formats;

internal static class BmpCodec
{
    private const int _headerLength = 54;

    private readonly record struct Header(
        int Width,
        int Height,
        bool TopDown,
        int BytesPerPixel,
        int Offset,
        int Stride
    );

    public static ImageInfo Identify(ReadOnlySpan<byte> data)
    {
        var header = ReadHeader(data);
        return new ImageInfo(header.Width, header.Height, ImageFormat.Bmp);
    }

    public static ImageBuffer Decode(ReadOnlySpan<byte> data)
    {
        var header = ReadHeader(data);
        var image = new ImageBuffer(header.Width, header.Height);
        for (var y = 0; y < header.Height; y++)
        {
            var sourceY = header.TopDown ? y : header.Height - y - 1;
            var row = data.Slice(header.Offset + sourceY * header.Stride, header.Stride);
            var pixels = image.GetRowSpan(y);
            for (var x = 0; x < header.Width; x++)
            {
                var offset = x * header.BytesPerPixel;
                pixels[x] = new Color(
                    row[offset + 2],
                    row[offset + 1],
                    row[offset],
                    header.BytesPerPixel == 4 ? row[offset + 3] : (byte)255
                );
            }
        }

        return image;
    }

    public static byte[] Encode(ImageBuffer image, bool saveAlpha)
    {
        var bytesPerPixel = saveAlpha ? 4 : 3;
        var stride = (image.Width * bytesPerPixel + 3) & ~3;
        var data = new byte[checked(_headerLength + stride * image.Height)];
        "BM"u8.CopyTo(data);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(2), data.Length);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(10), _headerLength);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(18), image.Width);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(22), -image.Height);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(28), (ushort)(bytesPerPixel * 8));
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(34), stride * image.Height);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(38), 3780);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(42), 3780);
        for (var y = 0; y < image.Height; y++)
        {
            var row = data.AsSpan(_headerLength + y * stride, stride);
            var pixels = image.GetRowSpan(y);
            for (var x = 0; x < image.Width; x++)
            {
                var offset = x * bytesPerPixel;
                row[offset] = pixels[x].B;
                row[offset + 1] = pixels[x].G;
                row[offset + 2] = pixels[x].R;
                if (saveAlpha)
                {
                    row[offset + 3] = pixels[x].A;
                }
            }
        }

        return data;
    }

    private static Header ReadHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < _headerLength || !data.StartsWith("BM"u8))
        {
            throw new InvalidDataException("Invalid BMP header.");
        }

        var headerSize = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(14, 4));
        var width = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(18, 4));
        var signedHeight = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(22, 4));
        var planes = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(26, 2));
        var bitsPerPixel = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(28, 2));
        var compression = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(30, 4));
        if (headerSize < 40 ||
            14L + headerSize > data.Length ||
            planes != 1 ||
            bitsPerPixel is not (24 or 32) ||
            compression != 0 ||
            signedHeight == int.MinValue)
        {
            throw new InvalidDataException(
                "Unsupported or invalid BMP header. Expected uncompressed 24-bit or 32-bit pixels."
            );
        }

        var height = Math.Abs(signedHeight);
        ImageBuffer.ValidateDimensions(width, height);
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(10, 4));
        var bytesPerPixel = bitsPerPixel / 8;
        var stride = (width * bytesPerPixel + 3) & ~3;
        if (offset < 14L + headerSize || offset + (long)stride * height > data.Length)
        {
            throw new InvalidDataException("Invalid BMP pixel offset or truncated pixel data.");
        }

        return new Header(width, height, signedHeight < 0, bytesPerPixel, (int)offset, stride);
    }
}
