using System.Text;

using Engine.Core.Mathematics;
using Engine.Graphics;
using Engine.Media.Images;

namespace Engine.Test.Graphics;

public sealed class BitmapFontStorageTest
{
    [Fact]
    public void OrdinaryTextureRetainsItsSourceForCpuConsumers()
    {
        using var source = CreateGlyphImage();
        using var texture = Texture2D.Load(source);
        Assert.Same(source, texture.Tag);
        Assert.Equal(Color.White, source.GetPixel(2, 2));
    }

    [Fact]
    public void GeneratedTextureDoesNotRetainPixelsOrDisposeTheCallersImage()
    {
        using var source = CreateGlyphImage();
        using var font = BitmapFont.Load(source, 'a', 'a', Vector2.Zero, 1, Vector2.Zero);
        Assert.NotNull(font.Texture);
        Assert.Null(font.Texture.Tag);
        Assert.Null(font.image);
        Assert.Equal(3, font.GetGlyph('a').Width);
        Assert.Equal(Color.White, source.GetPixel(2, 2));
    }

    [Fact]
    public void ImageOnlyFontRetainsItsAtlasForCpuConsumers()
    {
        using var source = CreateGlyphImage();
        using var font = BitmapFont.InternalLoad(source, 'a', 'a', Vector2.Zero, 1, Vector2.Zero, 1, false);
        Assert.Null(font.Texture);
        Assert.NotNull(font.image);
        Assert.Equal(Color.White, font.image.GetPixel(2, 2));
        Assert.Equal(Color.Transparent, font.image.GetPixel(0, 0));
    }

    [Fact]
    public void PrecomputedGlyphsKeepTheirMetricsWithoutRetainingDecodedPixels()
    {
        using var image = CreateGlyphImage();
        using var encoded = new MemoryStream();
        Image.Save(image, encoded, ImageFormat.Png, true);
        encoded.Position = 0;
        using var glyphs = new MemoryStream(Encoding.UTF8.GetBytes("1\na 0 0 1 1 0 0 3\n3\n0 0\n1\na\n"));
        using var font = BitmapFont.Initialize(encoded, glyphs);
        Assert.NotNull(font.Texture);
        Assert.Null(font.Texture.Tag);
        Assert.Null(font.image);
        Assert.Equal(3, font.GetGlyph('a').Width);
        Assert.Equal(3, font.GlyphHeight);
    }

    private static Image CreateGlyphImage()
    {
        var image = new Image(5, 5);
        Array.Fill(image.Pixels, Color.Magenta);
        for (var x = 1; x < 4; x++)
        {
            for (var y = 1; y < 4; y++) { image.SetPixel(x, y, Color.White); }
        }

        return image;
    }
}
