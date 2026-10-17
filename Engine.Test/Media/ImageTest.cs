using Engine.Core;
using Engine.Media;

namespace Engine.Test.Media;

public class ImageTest
{
    [Fact]
    public void PixelArrayChangesAreVisibleToGetPixelAndEncoding()
    {
        using var image = new Image(1, 1);
        image.Pixels[0] = Color.Red;
        Assert.Equal(Color.Red, image.GetPixel(0, 0));
        using var stream = new MemoryStream();
        Image.Save(image, stream, ImageFileFormat.Png, true);
        stream.Position = 0;
        var info = Png.GetInfo(stream);
        Assert.Equal(1, info.Width);
        Assert.Equal(1, info.Height);
        using var loaded = Image.Load(stream);
        Assert.Equal(Color.Red, loaded.GetPixel(0, 0));
    }

    [Fact]
    public void TiffIsRejectedBeforeDecoding()
    {
        using var stream = new MemoryStream([73, 73, 42, 0, 8, 0, 0, 0]);
        Assert.Throws<InvalidDataException>(() => Image.Load(stream));
        stream.Position = 0;
        Assert.Throws<InvalidDataException>(() => Png.GetInfo(stream));
        Assert.Throws<InvalidOperationException>(() => Image.DetermineFileFormat(".tiff"));
    }

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.WebP)]
    public void LosslessCodecsPreserveVisibleRgbaPixels(ImageFormat format)
    {
        using var source = new ImageBuffer(2, 2);
        source.Pixels[0] = new Color(19, 78, 155, 255);
        source.Pixels[1] = new Color(40, 120, 190, 128);
        source.Pixels[2] = new Color(211, 22, 59, 17);
        source.Pixels[3] = Color.Transparent;
        var bytes = ImageCodec.Encode(source, format);
        using var restored = ImageCodec.Decode(bytes);
        Assert.Equal(source.Width, restored.Width);
        Assert.Equal(source.Height, restored.Height);
        Assert.Equal(source.Pixels, restored.Pixels);
    }

    [Fact]
    public void JpegIsRejectedBeforeDecoding()
    {
        byte[] header = [255, 216, 255, 224, 0, 16];
        Assert.Throws<InvalidDataException>(() => ImageCodec.Identify(header));
        Assert.Throws<InvalidDataException>(() => ImageCodec.Decode(header));
        using var stream = new MemoryStream(header);
        Assert.Throws<InvalidDataException>(() => Image.Load(stream));
        Assert.Throws<InvalidOperationException>(() => Image.DetermineFileFormat(".jpg"));
        Assert.Throws<InvalidOperationException>(() => Image.DetermineFileFormat(".jpeg"));
    }

    [Fact]
    public void OpaqueEncodingDoesNotMutateSourceAlpha()
    {
        using var source = new ImageBuffer(1, 1);
        source.Pixels[0] = new Color(30, 100, 170, 123);
        foreach (var format in new[] { ImageFormat.Png, ImageFormat.WebP })
        {
            using var restored = ImageCodec.Decode(ImageCodec.Encode(source, format, false));
            Assert.Equal(255, restored.Pixels[0].A);
            Assert.Equal(123, source.Pixels[0].A);
        }
    }

    [Fact]
    public void PremultiplyAndCloneUseOneCoherentPixelBuffer()
    {
        using var source = new Image(1, 1);
        source.Pixels[0] = new Color(120, 60, 30, 128);
        using var clone = new Image(source);
        Image.PremultiplyAlpha(clone);
        Assert.Equal(new Color(60, 30, 15, 128), clone.Pixels[0]);
        Assert.Equal(new Color(120, 60, 30, 128), source.Pixels[0]);
    }

    [Fact]
    public void DecodingSupportsNonSeekableAndShortReadStreams()
    {
        using var source = new ImageBuffer(2, 2);
        source.Pixels[0] = Color.Red;
        using var stream = new ShortReadStream(ImageCodec.Encode(source, ImageFormat.Png));
        using var result = ImageCodec.Decode(stream);
        Assert.Equal(Color.Red, result.Pixels[0]);
    }

    [Fact]
    public void TruncatedPixelsAndOversizedDimensionsAreRejected()
    {
        using var source = new ImageBuffer(2, 2);
        var encoded = ImageCodec.Encode(source, ImageFormat.Png);
        Assert.Throws<InvalidDataException>(() => ImageCodec.Decode(encoded[..^8]));
        encoded[16] = 127;
        Assert.Throws<InvalidDataException>(() => ImageCodec.Decode(encoded));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BmpRoundTripPreservesRowOrder(bool alpha)
    {
        using var source = new Image(2, 2);
        source.Pixels[0] = Color.Red;
        source.Pixels[1] = Color.Green;
        source.Pixels[2] = Color.Blue;
        source.Pixels[3] = Color.White;
        using var stream = new MemoryStream();
        Image.Save(source, stream, ImageFileFormat.Bmp, alpha);
        stream.Position = 0;
        using var decoded = Image.Load(stream);
        Assert.Equal(source.Pixels, decoded.Pixels);
        using var shortStream = new ShortReadStream(stream.ToArray());
        using var fromShortStream = Image.Load(shortStream, ImageFileFormat.Bmp);
        Assert.Equal(source.Pixels, fromShortStream.Pixels);
    }

    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
        public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, System.Math.Min(count, 3));
        public override int Read(Span<byte> buffer) => base.Read(buffer[..System.Math.Min(buffer.Length, 3)]);
    }
}
