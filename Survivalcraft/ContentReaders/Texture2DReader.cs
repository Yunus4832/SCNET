using Engine.Graphics;
using Engine.Media;

namespace Game.ContentReaders;

public class Texture2DReader : IContentReader
{
    public override string Type => "Engine.Graphics.Texture2D";

    public override string[] DefaultSuffix => ["png", "jpg", "jpeg"];

    public override object Get(ContentInfo[] contents)
    {
        var image = Image.Load(contents[0].Duplicate());
        if (ShouldAutoPremultiplyAlpha(contents[0], image))
        {
            Image.PremultiplyAlpha(image);
        }

        return Texture2D.Load(image);
    }

    private static bool ShouldAutoPremultiplyAlpha(ContentInfo content, Image image)
    {
        if (!content.Filename.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var pixel in image.Pixels)
        {
            if (pixel.A is > 0 and < 255 && MathUtils.Max(pixel.R, pixel.G, pixel.B) > pixel.A + 1)
            {
                return true;
            }
        }

        return false;
    }
}
