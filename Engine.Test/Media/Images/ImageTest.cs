using System.Buffers.Binary;

using Engine.Core.Mathematics;
using Engine.Media.Images;

namespace Engine.Test.Media.Images;

public class ImageTest
{
    [Fact]
    public void PixelArrayChangesAreVisibleToGetPixelAndEncoding()
    {
        using var image = new Image(1, 1);
        image.Pixels[0] = Color.Red;
        Assert.Equal(Color.Red, image.GetPixel(0, 0));
        using var stream = new MemoryStream();
        Image.Save(image, stream, ImageFormat.Png, true);
        stream.Position = 0;
        var info = ImageCodec.Identify(stream);
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
        Assert.Throws<InvalidDataException>(() => ImageCodec.Identify(stream));
        Assert.Throws<InvalidOperationException>(() => ImageCodec.DetectFormat(".tiff"));
    }

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.WebP)]
    [InlineData(ImageFormat.Bmp)]
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
        Assert.Throws<InvalidOperationException>(() => ImageCodec.DetectFormat(".jpg"));
        Assert.Throws<InvalidOperationException>(() => ImageCodec.DetectFormat(".jpeg"));
    }

    [Fact]
    public void OpaqueEncodingDoesNotMutateSourceAlpha()
    {
        using var source = new ImageBuffer(1, 1);
        source.Pixels[0] = new Color(30, 100, 170, 123);
        foreach (var format in new[] { ImageFormat.Png, ImageFormat.WebP, ImageFormat.Bmp })
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

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.WebP)]
    [InlineData(ImageFormat.Bmp)]
    public void DecodingSupportsNonSeekableAndShortReadStreams(ImageFormat format)
    {
        using var source = new ImageBuffer(2, 2);
        source.Pixels[0] = Color.Red;
        var bytes = ImageCodec.Encode(source, format);
        using var stream = new ShortReadStream(bytes);
        using var result = ImageCodec.Decode(stream);
        Assert.Equal(Color.Red, result.Pixels[0]);
        Assert.True(stream.CanRead);
        using var imageStream = new ShortReadStream(bytes);
        using var image = Image.Load(imageStream, format);
        Assert.Equal(source.Pixels, image.Pixels);
        using var infoStream = new ShortReadStream(bytes);
        Assert.Equal(format, ImageCodec.Identify(infoStream).Format);
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
        Image.Save(source, stream, ImageFormat.Bmp, alpha);
        stream.Position = 0;
        using var decoded = Image.Load(stream);
        Assert.Equal(source.Pixels, decoded.Pixels);
        using var shortStream = new ShortReadStream(stream.ToArray());
        using var fromShortStream = Image.Load(shortStream, ImageFormat.Bmp);
        Assert.Equal(source.Pixels, fromShortStream.Pixels);
    }

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.WebP)]
    [InlineData(ImageFormat.Bmp)]
    public void MetadataPreservesStreamPositionAndExpectedFormatIsEnforced(ImageFormat format)
    {
        using var source = new ImageBuffer(1, 2);
        var encoded = ImageCodec.Encode(source, format);
        using var stream = new MemoryStream();
        stream.Write(new byte[7]);
        stream.Write(encoded);
        stream.Position = 7;
        Assert.Equal(new ImageInfo(1, 2, format), ImageCodec.Identify(stream));
        Assert.Equal(7, stream.Position);
        using var decoded = Image.Load(stream, format);
        Assert.Equal(source.Pixels, decoded.Pixels);
        Assert.True(stream.CanRead);
        var wrongFormat = format == ImageFormat.Png ? ImageFormat.WebP : ImageFormat.Png;
        Assert.Throws<FormatException>(() => ImageCodec.Decode(encoded, wrongFormat));
        stream.Position = 7;
        Assert.Throws<FormatException>(() => Image.Load(stream, wrongFormat));
    }

    [Theory]
    [InlineData(false, 24)]
    [InlineData(true, 24)]
    [InlineData(false, 32)]
    [InlineData(true, 32)]
    public void BmpHonorsPixelOffsetRowPaddingAndOrientation(bool topDown, int bitsPerPixel)
    {
        var encoded = CreateBmpFixture(topDown, bitsPerPixel);
        Assert.Equal(new ImageInfo(1, 2, ImageFormat.Bmp), ImageCodec.Identify(encoded));
        using var decoded = ImageCodec.Decode(encoded);
        var first = new Color(33, 22, 11, bitsPerPixel == 32 ? 44 : 255);
        var second = new Color(77, 66, 55, bitsPerPixel == 32 ? 88 : 255);
        Assert.Equal(topDown ? first : second, decoded.Pixels[0]);
        Assert.Equal(topDown ? second : first, decoded.Pixels[1]);
    }

    [Theory]
    [InlineData(10, 53)]
    [InlineData(10, int.MaxValue)]
    [InlineData(14, 12)]
    [InlineData(18, 32769)]
    [InlineData(22, int.MinValue)]
    [InlineData(26, 0)]
    [InlineData(28, 16)]
    [InlineData(30, 1)]
    public void BmpRejectsInvalidHeadersBeforeAllocatingPixels(int offset, int value)
    {
        var encoded = CreateBmpFixture(false, 24);
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(offset), value);
        Assert.Throws<InvalidDataException>(() => ImageCodec.Identify(encoded));
        Assert.Throws<InvalidDataException>(() => ImageCodec.Decode(encoded));
    }

    [Fact]
    public void FailedBmpInspectionRestoresTheStreamAndRejectsTruncatedPixels()
    {
        var encoded = CreateBmpFixture(false, 24)[..^1];
        using var stream = new MemoryStream(encoded);
        Assert.Throws<InvalidDataException>(() => ImageCodec.Identify(stream));
        Assert.Equal(0, stream.Position);
        Assert.Throws<InvalidDataException>(() => ImageCodec.Decode(encoded));
    }

    private static byte[] CreateBmpFixture(bool topDown, int bitsPerPixel)
    {
        // One pixel per row, six bytes between the DIB header and pixels.
        // The fourth byte is padding for 24-bit BMP and alpha for 32-bit BMP.
        var encoded = new byte[68];
        "BM"u8.CopyTo(encoded);
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(2), encoded.Length);
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(10), 60);
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(14), 40);
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(18), 1);
        BinaryPrimitives.WriteInt32LittleEndian(encoded.AsSpan(22), topDown ? -2 : 2);
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(28), (ushort)bitsPerPixel);
        encoded.AsSpan(54, 6).Fill(0xdd);
        new byte[] { 11, 22, 33, 44, 55, 66, 77, 88 }.CopyTo(encoded, 60);
        return encoded;
    }

    private sealed class ShortReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;

        public override int Read(byte[] buffer, int offset, int count) =>
            base.Read(buffer, offset, System.Math.Min(count, 3));

        public override int Read(Span<byte> buffer) => base.Read(buffer[..System.Math.Min(buffer.Length, 3)]);
    }
}
