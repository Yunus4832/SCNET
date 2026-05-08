using SixLabors.ImageSharp.PixelFormats;

namespace Engine.Media;

public static class Rgba32Extensions
{
    public static Rgba32 PremultiplyAlpha(this Rgba32 pixel) => new(
        (byte)(pixel.R * (uint)pixel.A / 255u),
        (byte)(pixel.G * (uint)pixel.A / 255u),
        (byte)(pixel.B * (uint)pixel.A / 255u),
        pixel.A
    );

    public static bool IsMagenta(this Rgba32 pixel) => pixel is { R: 255, G: 0, B: 255, A: 255 };

    public static bool IsCompletelyTransparent(this Rgba32 pixel) => pixel is { R: 0, G: 0, B: 0, A: 0 };
}
