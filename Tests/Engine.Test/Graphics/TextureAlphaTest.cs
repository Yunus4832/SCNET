using Engine.Core.Mathematics;
using Engine.Graphics;
using Engine.Media.Images;

namespace Engine.Test.Graphics;

public sealed class TextureAlphaTest
{
    [Fact]
    public void UploadPremultipliesEveryAlphaWithoutChangingTheSource()
    {
        using var source = new Image(256, 256);
        for (var alpha = 0; alpha < 256; alpha++)
        {
            for (var channel = 0; channel < 256; channel++)
            {
                source.Pixels[alpha * 256 + channel] = new Color(channel, 255 - channel, channel, alpha);
            }
        }

        using var prepared = Texture2D.PrepareImageForUpload(source);
        for (var alpha = 0; alpha < 256; alpha++)
        {
            for (var channel = 0; channel < 256; channel++)
            {
                var index = alpha * 256 + channel;
                Assert.Equal(new Color(channel, 255 - channel, channel, alpha), source.Pixels[index]);
                Assert.Equal(
                    new Color(channel * alpha / 255, (255 - channel) * alpha / 255, channel * alpha / 255, alpha),
                    prepared.Pixels[index]
                );
            }
        }
    }

    [Fact]
    public void ExportRestoresStraightAlphaAndClearsInvisibleColors()
    {
        using var image = new Image(3, 1);
        image.Pixels[0] = new Color(60, 30, 15, 128);
        image.Pixels[1] = new Color(12, 34, 56, 255);
        image.Pixels[2] = new Color(12, 34, 56, 0);
        Image.UnpremultiplyAlpha(image);
        Assert.Equal(new Color(120, 60, 30, 128), image.Pixels[0]);
        Assert.Equal(new Color(12, 34, 56, 255), image.Pixels[1]);
        Assert.Equal(Color.Transparent, image.Pixels[2]);
    }

    [Fact]
    public void StraightBlendingAccumulatesCoverageWithoutSquaringAlpha()
    {
        Assert.Equal(Blend.SourceAlpha, BlendState.NonPremultiplied.ColorSourceBlend);
        Assert.Equal(Blend.One, BlendState.NonPremultiplied.AlphaSourceBlend);
        Assert.Equal(Blend.One, BlendState.PremultipliedAdditive.ColorSourceBlend);
    }
}
