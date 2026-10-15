using System.Buffers.Binary;

using Engine.Media.Native;

namespace Engine.Media;

public static class ImageCodec
{
    private const int _maximumEncodedBytes = 512 * 1024 * 1024;

    public static ImageFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[137, 80, 78, 71, 13, 10, 26, 10]))
        {
            return ImageFormat.Png;
        }
        if (data.Length >= 3 && data[0] == 255 && data[1] == 216 && data[2] == 255)
        {
            return ImageFormat.Jpeg;
        }
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ImageFormat.WebP;
        }
        throw new InvalidDataException("Unsupported image format. Expected PNG, JPEG or WebP.");
    }

    public static ImageInfo Identify(ReadOnlySpan<byte> data)
    {
        var info = DetectFormat(data) switch
        {
            ImageFormat.Png => IdentifyPng(data),
            ImageFormat.Jpeg => JpegCodec.Identify(data),
            ImageFormat.WebP => WebPCodec.Identify(data),
            _ => throw new InvalidDataException("Unsupported image format.")
        };
        ImageBuffer.ValidateDimensions(info.Width, info.Height);
        return info;
    }

    public static ImageInfo Identify(Stream stream)
    {
        var position = stream.CanSeek ? stream.Position : 0;
        try
        {
            return Identify(ReadEncoded(stream));
        }
        finally
        {
            if (stream.CanSeek)
            {
                stream.Position = position;
            }
        }
    }

    public static ImageBuffer Decode(ReadOnlySpan<byte> data)
    {
        var info = Identify(data);
        if (info.Frames != 1)
        {
            throw new InvalidDataException("Animated images are not supported.");
        }
        return info.Format switch
        {
            ImageFormat.Png => PngCodec.Decode(data),
            ImageFormat.Jpeg => JpegCodec.Decode(data, info),
            ImageFormat.WebP => WebPCodec.Decode(data, info),
            _ => throw new InvalidDataException("Unsupported image format.")
        };
    }

    public static ImageBuffer Decode(Stream stream) => Decode(ReadEncoded(stream));

    public static byte[] Encode(ImageBuffer image, ImageFormat format, bool saveAlpha = true, int quality = 95)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (quality is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(quality));
        }
        if (format == ImageFormat.WebP && !saveAlpha)
        {
            using var opaque = image.Clone();
            for (var index = 0; index < opaque.Pixels.Length; index++)
            {
                opaque.Pixels[index].A = 255;
            }
            return WebPCodec.Encode(opaque);
        }
        return format switch
        {
            ImageFormat.Png => PngCodec.Encode(image, saveAlpha),
            ImageFormat.Jpeg => JpegCodec.Encode(image, quality),
            ImageFormat.WebP => WebPCodec.Encode(image),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public static void Encode(ImageBuffer image, Stream stream, ImageFormat format, bool saveAlpha = true, int quality = 95)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(Encode(image, format, saveAlpha, quality));
    }

    private static byte[] ReadEncoded(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (stream.CanSeek && stream.Length - stream.Position > _maximumEncodedBytes)
        {
            throw new InvalidDataException("Encoded image exceeds the size limit.");
        }
        using var memory = new MemoryStream();
        var buffer = new byte[64 * 1024];
        int read;
        while ((read = stream.Read(buffer)) > 0)
        {
            if (memory.Length + read > _maximumEncodedBytes)
            {
                throw new InvalidDataException("Encoded image exceeds the size limit.");
            }
            memory.Write(buffer, 0, read);
        }
        return memory.ToArray();
    }

    private static ImageInfo IdentifyPng(ReadOnlySpan<byte> data)
    {
        if (data.Length < 33 || BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4)) != 13 ||
            !data.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("Invalid PNG header.");
        }
        var width = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
        if (width > int.MaxValue || height > int.MaxValue)
        {
            throw new InvalidDataException("Invalid PNG dimensions.");
        }
        var frames = 1;
        var offset = 8;
        var sawEnd = false;
        while (offset <= data.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            if (length > int.MaxValue || (long)offset + length + 12 > data.Length)
            {
                throw new InvalidDataException("Truncated PNG chunk.");
            }
            var type = data.Slice(offset + 4, 4);
            if (type.SequenceEqual("acTL"u8))
            {
                if (length != 8)
                {
                    throw new InvalidDataException("Invalid PNG animation header.");
                }
                frames = Math.Max(2, (int)Math.Min(int.MaxValue, BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset + 8, 4))));
            }
            if (type.SequenceEqual("IEND"u8))
            {
                sawEnd = true;
                break;
            }
            offset = checked(offset + (int)length + 12);
        }
        if (!sawEnd)
        {
            throw new InvalidDataException("PNG end chunk is missing.");
        }
        return new ImageInfo((int)width, (int)height, ImageFormat.Png, frames);
    }
}
