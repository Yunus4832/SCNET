using Engine.Core.Mathematics;

namespace Engine.Media.Images;

public sealed class Image : IDisposable
{
    public ImageBuffer Buffer { get; }

    public int Width => Buffer.Width;

    public int Height => Buffer.Height;

    public Color[] Pixels => Buffer.Pixels;

    public bool IsDisposed => Buffer.IsDisposed;

    public Image() : this(1, 1)
    {
    }

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
                pixel.R * pixel.A / 255,
                pixel.G * pixel.A / 255,
                pixel.B * pixel.A / 255,
                pixel.A
            );
        }
    }

    public static Image Load(Stream stream) => new(ImageCodec.Decode(stream));

    public static Image Load(Stream stream, ImageFormat format) => new(ImageCodec.Decode(stream, format));

    public static void Save(Image image, Stream stream, ImageFormat format, bool saveAlpha)
    {
        ArgumentNullException.ThrowIfNull(image);
        ImageCodec.Encode(image.Buffer, stream, format, saveAlpha);
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
