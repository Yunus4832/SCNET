using System.Runtime.InteropServices;

using Engine.Core;

namespace Engine.Media;

/// <summary>Owns top-down RGBA8 pixels with straight alpha.</summary>
public sealed class ImageBuffer : IDisposable
{
    private Color[]? _pixels;

    public int Width { get; }
    public int Height { get; }
    public int Stride => checked(Width * 4);
    public bool IsDisposed => _pixels is null;
    public Color[] Pixels => _pixels ?? throw new ObjectDisposedException(nameof(ImageBuffer));
    public Span<byte> Bytes => MemoryMarshal.AsBytes(Pixels.AsSpan());

    public ImageBuffer(int width, int height)
    {
        ValidateDimensions(width, height);
        Width = width;
        Height = height;
        _pixels = new Color[checked(width * height)];
    }

    public Span<Color> GetRowSpan(int y)
    {
        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }
        return Pixels.AsSpan(y * Width, Width);
    }

    public ImageBuffer Clone()
    {
        var clone = new ImageBuffer(Width, Height);
        Pixels.CopyTo(clone.Pixels, 0);
        return clone;
    }

    internal static void ValidateDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0 || width > 32768 || height > 32768 || (long)width * height > 64 * 1024 * 1024)
        {
            throw new InvalidDataException("Image dimensions exceed the supported pixel limit.");
        }
    }

    public void Dispose() => _pixels = null;
}
