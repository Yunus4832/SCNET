using System.Globalization;

using SkiaSharp;

namespace GuiAtlasTool.Test;

public sealed class AtlasAlphaTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "scnet-atlas-" + Guid.NewGuid().ToString("N"));

    public AtlasAlphaTest()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void MixedSourcesProduceStraightLosslessWebPAndExtractWithoutPremultiplying()
    {
        var assets = Path.Combine(_root, "sources");
        Directory.CreateDirectory(assets);
        using var source = new SKBitmap(new SKImageInfo(2, 2, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        source.Erase(new SKColor(240, 120, 60, 128));
        using (var pixels = source.PeekPixels())
        {
            using var png = File.Create(Path.Combine(assets, "png.png"));
            using var pngStream = new SKManagedWStream(png);
            Assert.True(pixels.Encode(pngStream, new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, 9)));
            using var webp = File.Create(Path.Combine(assets, "webp.webp"));
            using var webpStream = new SKManagedWStream(webp);
            Assert.True(pixels.Encode(webpStream, new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 100)));
        }

        File.WriteAllText(
            Path.Combine(assets, "svg.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"2\" height=\"2\" viewBox=\"0 0 2 2\"><rect width=\"2\" height=\"2\" fill=\"#f0783c\" fill-opacity=\"0.5\"/></svg>"
        );
        var atlas = Path.Combine(_root, "atlas.webp");
        var mapping = Path.Combine(_root, "atlas.txt");
        Assert.Equal(0, GuiAtlasProgram.Run(["generate", assets, atlas, mapping]));
        var writtenAt = File.GetLastWriteTimeUtc(atlas);
        Parallel.For(0, 8, _ => Assert.Equal(0, GuiAtlasProgram.Run(["generate", assets, atlas, mapping])));
        Assert.Equal(writtenAt, File.GetLastWriteTimeUtc(atlas));
        using var atlasData = SKData.CreateCopy(File.ReadAllBytes(atlas));
        using var codec = SKCodec.Create(atlasData);
        Assert.Equal(SKEncodedImageFormat.Webp, codec.EncodedFormat);
        using var decoded = new SKBitmap(
            new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul)
        );
        Assert.Equal(SKCodecResult.Success, codec.GetPixels(decoded.Info, decoded.GetPixels()));
        var entries = File.ReadAllLines(mapping);
        Assert.Equal(3, entries.Length);
        foreach (var line in entries)
        {
            var values = line.Split(' ');
            var x = int.Parse(values[1], CultureInfo.InvariantCulture);
            var y = int.Parse(values[2], CultureInfo.InvariantCulture);
            var pixel = decoded.GetPixel(x, y);
            Assert.InRange(pixel.Alpha, 127, 128);
            Assert.InRange(pixel.Red, 238, 242);
        }

        var extracted = Path.Combine(_root, "extracted");
        Assert.Equal(0, GuiAtlasProgram.Run(["extract", atlas, mapping, extracted, "png", "webp"]));
        using var extractedCodec = SKCodec.Create(Path.Combine(extracted, "png.webp"));
        using var extractedImage = new SKBitmap(source.Info);
        Assert.Equal(SKCodecResult.Success, extractedCodec.GetPixels(extractedImage.Info, extractedImage.GetPixels()));
        Assert.Equal(new SKColor(240, 120, 60, 128), extractedImage.GetPixel(0, 0));

        File.Delete(Path.Combine(assets, "svg.svg"));
        Assert.Equal(0, GuiAtlasProgram.Run(["generate", assets, atlas, mapping]));
        Assert.Equal(2, File.ReadAllLines(mapping).Length);
    }

    [Fact]
    public void DuplicateNamesAndPngOutputAreRejected()
    {
        var assets = Path.Combine(_root, "sources");
        Directory.CreateDirectory(assets);
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"/>";
        File.WriteAllText(Path.Combine(assets, "entry.svg"), svg);
        using var image = new SKBitmap(1, 1);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using (var file = File.Create(Path.Combine(assets, "entry.png")))
        {
            data.SaveTo(file);
        }

        var mapping = Path.Combine(_root, "atlas.txt");
        Assert.Equal(1, GuiAtlasProgram.Run(["generate", assets, Path.Combine(_root, "atlas.webp"), mapping]));
        Assert.Equal(1, GuiAtlasProgram.Run(["generate", assets, Path.Combine(_root, "atlas.png"), mapping]));
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }
}
