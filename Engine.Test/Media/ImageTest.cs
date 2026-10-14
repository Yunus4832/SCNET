using Engine.Core;
using Engine.Media;

namespace Engine.Test.Media;

public class ImageTest
{
    [Fact]
    public void SaveFlushesPixelsCache()
    {
        var image = new Image(1, 1)
        {
            Pixels =
            {
                [0] = Color.Red
            }
        };

        using var stream = new MemoryStream();
        Image.Save(image, stream, ImageFileFormat.Png, true, sync: true);
        stream.Position = 0;

        var info = Png.GetInfo(stream);
        Assert.Equal(1, info.Width);
        Assert.Equal(1, info.Height);
        stream.Position = 0;
        var loaded = Image.Load(stream);

        Assert.Equal(Color.Red.PackedValue, loaded.GetPixel(0, 0).PackedValue);
    }
    [Fact]
    public void TiffIsRejectedBeforeDecoding()
    {
        using var source = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1, 1);
        using var stream = new MemoryStream();
        source.Save(stream, new SixLabors.ImageSharp.Formats.Tiff.TiffEncoder());
        stream.Position = 0;

        Assert.Throws<SixLabors.ImageSharp.UnknownImageFormatException>(() => Image.Load(stream));
        stream.Position = 0;
        Assert.Throws<FormatException>(() => Png.GetInfo(stream));
        Assert.Throws<InvalidOperationException>(() => Image.DetermineFileFormat(".tiff"));
    }

    [Fact]
    public void WebpRoundTripsPixels()
    {
        var image = new Image(2, 2);
        using var sourcePixels = image.TrueImage;
        image.SetPixel(0, 0, Color.Red);
        using var stream = new MemoryStream();
        Image.Save(image, stream, ImageFileFormat.WebP, true, sync: true);
        stream.Position = 0;
        var restored = Image.Load(stream);
        using var restoredPixels = restored.TrueImage;

        Assert.Equal(2, restored.Width);
        Assert.Equal(2, restored.Height);
        Assert.Equal(Color.Red.PackedValue, restored.GetPixel(0, 0).PackedValue);
    }

}
