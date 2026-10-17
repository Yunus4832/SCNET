using Engine.Core;

namespace Engine.Media;

public sealed class Image : IDisposable
{
    public ImageBuffer Buffer { get; }
    public int Width => Buffer.Width;
    public int Height => Buffer.Height;
    public Color[] Pixels => Buffer.Pixels;
    public bool IsDisposed => Buffer.IsDisposed;

    public Image() : this(1, 1) { }

    public Image(int width, int height) => Buffer = new ImageBuffer(width, height);

    public Image(Image image) => Buffer = image.Buffer.Clone();

    public Image(ImageBuffer buffer) => Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

    public Color GetPixel(int x, int y)
    {
        ValidateCoordinates(x, y);
        return Pixels[y * Width + x];
    }

    public void SetPixel(int x, int y, Color color)
    {
        ValidateCoordinates(x, y);
        Pixels[y * Width + x] = color;
    }

    public static void PremultiplyAlpha(Image image)
    {
        for (var index = 0; index < image.Pixels.Length; index++)
        {
            var pixel = image.Pixels[index];
            image.Pixels[index] = new Color(
                pixel.R * pixel.A / 255, pixel.G * pixel.A / 255, pixel.B * pixel.A / 255, pixel.A);
        }
    }

    public static ImageFileFormat DetermineFileFormat(string extension) => extension.ToLowerInvariant() switch
    {
        ".png" => ImageFileFormat.Png,
        ".webp" => ImageFileFormat.WebP,
        ".bmp" => ImageFileFormat.Bmp,
        _ => throw new InvalidOperationException("Unsupported image file format.")
    };

    public static ImageFileFormat DetermineFileFormat(Stream stream)
    {
        if (stream.CanSeek && Bmp.IsBmpStream(stream))
        {
            return ImageFileFormat.Bmp;
        }
        return ImageCodec.Identify(stream).Format switch
        {
            ImageFormat.Png => ImageFileFormat.Png,
            ImageFormat.WebP => ImageFileFormat.WebP,
            _ => throw new InvalidDataException("Unsupported image format.")
        };
    }

    public static Image Load(Stream stream)
    {
        if (!stream.CanSeek)
        {
            using var buffered = BufferEncoded(stream);
            return Load(buffered);
        }
        if (stream.CanSeek && Bmp.IsBmpStream(stream))
        {
            return Bmp.Load(stream);
        }
        return new Image(ImageCodec.Decode(stream));
    }

    public static Image Load(Stream stream, ImageFileFormat format)
    {
        if (!stream.CanSeek)
        {
            using var buffered = BufferEncoded(stream);
            return Load(buffered, format);
        }
        if (DetermineFileFormat(stream) != format)
        {
            throw new FormatException($"Image does not have the requested format: {format}.");
        }
        return Load(stream);
    }

    public static void Save(Image image, Stream stream, ImageFileFormat format, bool saveAlpha)
    {
        if (format == ImageFileFormat.Bmp)
        {
            Bmp.Save(image, stream, saveAlpha ? Bmp.Format.RGBA8 : Bmp.Format.RGB8);
            return;
        }
        var codecFormat = format switch
        {
            ImageFileFormat.Png => ImageFormat.Png,
            ImageFileFormat.WebP => ImageFormat.WebP,
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        ImageCodec.Encode(image.Buffer, stream, codecFormat, saveAlpha);
    }

    private static MemoryStream BufferEncoded(Stream stream)
    {
        var result = new MemoryStream();
        try
        {
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = stream.Read(buffer)) > 0)
            {
                if (result.Length + read > 512 * 1024 * 1024)
                {
                    throw new InvalidDataException("Encoded image exceeds the size limit.");
                }
                result.Write(buffer, 0, read);
            }
            result.Position = 0;
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private void ValidateCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Pixel coordinates are outside the image.");
        }
    }

    public void Dispose() => Buffer.Dispose();
}
