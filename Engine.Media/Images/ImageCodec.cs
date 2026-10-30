using Engine.Media.Images.Formats;

namespace Engine.Media.Images;

public static class ImageCodec
{
    private const int _maximumEncodedBytes = 512 * 1024 * 1024;

    public static ImageFormat DetectFormat(string extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        return extension.ToLowerInvariant() switch
        {
            ".png" => ImageFormat.Png,
            ".webp" => ImageFormat.WebP,
            ".bmp" => ImageFormat.Bmp,
            _ => throw new InvalidOperationException("Unsupported image file format.")
        };
    }

    public static ImageFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[137, 80, 78, 71, 13, 10, 26, 10]))
        {
            return ImageFormat.Png;
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return ImageFormat.WebP;
        }

        if (data.StartsWith("BM"u8))
        {
            return ImageFormat.Bmp;
        }

        throw new InvalidDataException("Unsupported image format. Expected PNG, WebP or BMP.");
    }

    public static ImageInfo Identify(ReadOnlySpan<byte> data)
    {
        if (data.Length > _maximumEncodedBytes)
        {
            throw new InvalidDataException("Encoded image exceeds the size limit.");
        }

        var info = DetectFormat(data) switch
        {
            ImageFormat.Png => PngCodec.Identify(data),
            ImageFormat.WebP => WebPCodec.Identify(data),
            ImageFormat.Bmp => BmpCodec.Identify(data),
            _ => throw new InvalidDataException("Unsupported image format.")
        };
        ImageBuffer.ValidateDimensions(info.Width, info.Height);
        return info;
    }

    public static ImageInfo Identify(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
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

    public static ImageBuffer Decode(ReadOnlySpan<byte> data, ImageFormat? expectedFormat = null)
    {
        var info = Identify(data);
        if (expectedFormat is { } expected && info.Format != expected)
        {
            throw new FormatException($"Image does not have the requested format: {expected}.");
        }

        if (info.Frames != 1)
        {
            throw new InvalidDataException("Animated images are not supported.");
        }

        return info.Format switch
        {
            ImageFormat.Png => PngCodec.Decode(data),
            ImageFormat.WebP => WebPCodec.Decode(data, info),
            ImageFormat.Bmp => BmpCodec.Decode(data),
            _ => throw new InvalidDataException("Unsupported image format.")
        };
    }

    public static ImageBuffer Decode(Stream stream, ImageFormat? expectedFormat = null)
    {
        return Decode(ReadEncoded(stream), expectedFormat);
    }

    public static byte[] Encode(ImageBuffer image, ImageFormat format, bool saveAlpha = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        return format switch
        {
            ImageFormat.Png => PngCodec.Encode(image, saveAlpha),
            ImageFormat.WebP => WebPCodec.Encode(image, saveAlpha),
            ImageFormat.Bmp => BmpCodec.Encode(image, saveAlpha),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    public static void Encode(ImageBuffer image, Stream stream, ImageFormat format, bool saveAlpha = true)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(Encode(image, format, saveAlpha));
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
}
