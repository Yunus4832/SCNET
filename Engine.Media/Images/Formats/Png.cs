namespace Engine.Media.Images.Formats;

public static class Png
{
    public enum Format
    {
        RGBA8 = 0,

        RGB8 = 1,

        L8 = 2,

        LA8 = 3,

        Indexed = 4
    }

    public struct PngInfo
    {
        public int Width;

        public int Height;

        public Format Format;
    }

    public static bool IsPngStream(Stream stream) => ImageCodec.Identify(stream).Format == ImageFormat.Png;

    public static PngInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new ArgumentException("PNG metadata inspection requires a seekable stream.", nameof(stream));
        }

        var info = ImageCodec.Identify(stream);
        if (info.Format != ImageFormat.Png)
        {
            throw new FormatException("Image format is not PNG.");
        }

        var position = stream.Position;
        Span<byte> header = stackalloc byte[26];
        try
        {
            stream.ReadExactly(header);
        }
        finally
        {
            stream.Position = position;
        }

        return new PngInfo
        {
            Width = info.Width,
            Height = info.Height,
            Format = header[25] switch
            {
                0 => Format.L8,
                2 => Format.RGB8,
                3 => Format.Indexed,
                4 => Format.LA8,
                6 => Format.RGBA8,
                _ => throw new InvalidDataException("Unsupported PNG pixel format.")
            }
        };
    }

    public static Image Load(Stream stream) => Image.Load(stream, ImageFileFormat.Png);

    public static void Save(Image image, Stream stream, Format format)
    {
        if (format is not (Format.RGBA8 or Format.RGB8))
        {
            throw new NotSupportedException("PNG encoding supports RGB8 and RGBA8 pixels.");
        }

        ImageCodec.Encode(image.Buffer, stream, ImageFormat.Png, format == Format.RGBA8);
    }
}
