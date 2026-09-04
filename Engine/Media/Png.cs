using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Engine.Media;

public static class Png
{
    public enum Format
    {
        RGBA8,
        RGB8,
        L8,
        LA8,
        Indexed
    }

    public struct PngInfo
    {
        public int Width;

        public int Height;

        public Format Format;
    }

    public static bool IsPngStream(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return SixLabors.ImageSharp.Image.DetectFormat(stream).Name == "PNG";
    }

    public static PngInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var info = SixLabors.ImageSharp.Image.Identify(stream);
        if (info.Metadata.DecodedImageFormat?.Name != "PNG")
        {
            throw new FormatException(
                $"Image format({info.Metadata.DecodedImageFormat?.Name ?? string.Empty}) is not Png");
        }

        PngInfo result = default;
        result.Width = info.Width;
        result.Height = info.Height;
        result.Format = info.Metadata.GetPngMetadata().ColorType switch
        {
            PngColorType.RgbWithAlpha => Format.RGBA8,
            PngColorType.Rgb => Format.RGB8,
            PngColorType.GrayscaleWithAlpha => Format.LA8,
            PngColorType.Grayscale => Format.L8,
            PngColorType.Palette => Format.Indexed,
            _ => throw new InvalidOperationException("Unsupported PNG pixel format.")
        };
        return result;
    }

    public static Image Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var formatName = SixLabors.ImageSharp.Image.DetectFormat(stream).Name;
        return formatName != "PNG"
            ? throw new FormatException($"Image format({formatName}) is not Png")
            : Image.Load(stream);
    }

    public static void Save(Image image,
        Stream stream,
        Format format,
        PngCompressionLevel compressionLevel = PngCompressionLevel.DefaultCompression,
        bool sync = false
    )
    {
        var pngColorType = format switch
        {
            Format.RGBA8 => PngColorType.RgbWithAlpha,
            Format.RGB8 => PngColorType.Rgb,
            Format.LA8 => PngColorType.GrayscaleWithAlpha,
            Format.L8 => PngColorType.Grayscale,
            Format.Indexed => PngColorType.Palette,
            _ => throw new InvalidOperationException("Unsupported PNG pixel format.")
        };

        var encoder = new PngEncoder
        {
            ColorType = pngColorType,
            CompressionLevel = compressionLevel,
            TransparentColorMode = PngTransparentColorMode.Clear
        };
        if (sync)
        {
            image.TrueImage.SaveAsPng(stream, encoder);
        }
        else
        {
            image.TrueImage.SaveAsPngAsync(stream, encoder);
        }
    }
}
