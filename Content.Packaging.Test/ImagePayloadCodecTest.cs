using System.Buffers.Binary;
using System.Text.Json;

using Content.Packaging.Payloads;

using Engine.Core.Mathematics;
using Engine.Media.Images;

namespace Content.Packaging.Test;

public sealed class ImagePayloadCodecTest
{
    [Fact]
    public void LossyWebPIsAlsoAnAcceptedStaticImage()
    {
        var encoded = Convert.FromBase64String(
            "UklGRjgAAABXRUJQVlA4ICwAAADwAQCdASoEAAQAAgA0JaACdLoB+AAEyAAA/pcX/yC5YXXI1/5xKiCTeYAAAA=="
        );
        var info = ImagePayloadCodec.ValidateSource(encoded, ContentPackageType.BlocksTexture);
        Assert.Equal(ImageFormat.WebP, info.Format);
        Assert.Equal(4, info.Width);
    }

    [Fact]
    public void ValidInterlacedPngIsAccepted()
    {
        var encoded = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR4nGP4DwQACfsD/fteaysAAAAASUVORK5CYII="
        );
        encoded[28] = 1;
        var crc = uint.MaxValue;
        foreach (var value in encoded.AsSpan(12, 17))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0u : 0xedb88320u);
            }
        }

        BinaryPrimitives.WriteUInt32BigEndian(encoded.AsSpan(29), ~crc);
        var info = ImagePayloadCodec.ValidateSource(encoded, ContentPackageType.CharacterSkin);
        Assert.Equal(ImageFormat.Png, info.Format);
        Assert.Equal(1, info.Width);
    }

    [Theory]
    [InlineData(ContentPackageType.BlocksTexture, ImageFormat.Png)]
    [InlineData(ContentPackageType.BlocksTexture, ImageFormat.WebP)]
    [InlineData(ContentPackageType.CharacterSkin, ImageFormat.Png)]
    [InlineData(ContentPackageType.CharacterSkin, ImageFormat.WebP)]
    public void BothImageTypesAcceptPngAndWebP(ContentPackageType type, ImageFormat format)
    {
        var encoded = Encode(format);
        using var package = WritePackage(type, format, encoded);
        var inspection = ContentPackageReader.Inspect(package);
        Assert.Equal(ImagePayloadCodec.CreatePayload(type, format), inspection.Manifest.Payload);
        Assert.Equal(16, inspection.Manifest.Metadata.GetProperty("width").GetInt32());
    }

    [Theory]
    [InlineData(ImageFormat.Png, ImageFormat.WebP)]
    [InlineData(ImageFormat.WebP, ImageFormat.Png)]
    public void DeclaredEncodingMustMatchTheImage(ImageFormat declared, ImageFormat actual)
    {
        Assert.Throws<ContentPackageException>(() => WritePackage(
                ContentPackageType.BlocksTexture,
                declared,
                Encode(actual)
            )
        );
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("trailing")]
    [InlineData("wrong-length")]
    [InlineData("animation")]
    public void WebPContainersRejectMalformedOrAnimatedData(string mutation)
    {
        var bytes = Encode(ImageFormat.WebP);
        if (mutation == "truncated")
        {
            bytes = bytes[..^1];
        }
        else if (mutation == "trailing")
        {
            bytes = [.. bytes, 1, 2, 3, 4];
        }
        else if (mutation == "wrong-length")
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
        }
        else
        {
            bytes = [.. bytes, .. "ANIM"u8, 0, 0, 0, 0];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)bytes.Length - 8);
        }

        Assert.Throws<ContentPackageException>(() =>
            ImagePayloadCodec.ValidateSource(bytes, ContentPackageType.BlocksTexture)
        );
    }

    [Theory]
    [InlineData(ContentPackageType.BlocksTexture, 3)]
    [InlineData(ContentPackageType.BlocksTexture, 16384)]
    [InlineData(ContentPackageType.CharacterSkin, 2048)]
    public void DimensionLimitsStillApply(ContentPackageType type, int width)
    {
        using var image = new ImageBuffer(width, 1);
        var encoded = ImageCodec.Encode(image, ImageFormat.Png);
        Assert.Throws<ContentPackageException>(() => ImagePayloadCodec.ValidateSource(encoded, type));
    }

    private static byte[] Encode(ImageFormat format)
    {
        using var image = new ImageBuffer(16, 16);
        Array.Fill(image.Pixels, new Color(200, 100, 50, 128));
        return ImageCodec.Encode(image, format);
    }

    private static MemoryStream WritePackage(ContentPackageType type, ImageFormat declared, byte[] bytes)
    {
        var payload = ImagePayloadCodec.CreatePayload(type, declared);
        var manifest = new ContentPackageManifest(
            ContentPackageManifest.CurrentFormatVersion,
            type,
            Guid.NewGuid().ToString(),
            "Image test",
            "Summary",
            "Description",
            "1.0.0",
            payload,
            JsonSerializer.SerializeToElement(new { width = 16, height = 16 })
        );
        var package = new MemoryStream();
        try
        {
            ContentPackageWriter.Write(
                package,
                manifest,
                [new ContentPackageWriteEntry(payload.Entry, bytes.Length, () => new MemoryStream(bytes, false))]
            );
            package.Position = 0;
            return package;
        }
        catch
        {
            package.Dispose();
            throw;
        }
    }
}
