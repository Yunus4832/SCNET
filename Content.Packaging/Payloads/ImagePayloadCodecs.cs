using System.Runtime.InteropServices;

using Engine.Media.Images;

namespace Content.Packaging.Payloads;

public sealed class BlocksTexturePayloadCodec : ImagePayloadCodec
{
    public override ContentPackageType Type => ContentPackageType.BlocksTexture;
}

public sealed class CharacterSkinPayloadCodec : ImagePayloadCodec
{
    public override ContentPackageType Type => ContentPackageType.CharacterSkin;
}

public abstract class ImagePayloadCodec : IContentPayloadCodec
{
    public abstract ContentPackageType Type { get; }

    public static ImageFormat GetImageFormat(string mediaType) => mediaType switch
    {
        "image/png" => ImageFormat.Png,
        "image/webp" => ImageFormat.WebP,
        _ => throw new ContentPackageException("Image media type must be image/png or image/webp.")
    };

    public static ContentPackagePayload CreatePayload(ContentPackageType type, ImageFormat format)
    {
        var extension = format switch
        {
            ImageFormat.Png => "png",
            ImageFormat.WebP => "webp",
            _ => throw new ContentPackageException("Image source must be PNG or WebP.")
        };
        var (kind, name) = type switch
        {
            ContentPackageType.BlocksTexture => ("blocks-texture", "texture"),
            ContentPackageType.CharacterSkin => ("character-skin", "skin"),
            _ => throw new ContentPackageException("Image packages only support textures and skins.")
        };
        return new ContentPackagePayload(
            $"scnet.{kind}.{extension}-v1",
            $"payload/{name}.{extension}",
            $"image/{extension}"
        );
    }

    public static ImageInfo ValidateSource(Stream source, ContentPackageType type)
    {
        using var buffer = new MemoryStream();
        var bytes = new byte[64 * 1024];
        int read;
        while ((read = source.Read(bytes)) > 0)
        {
            if (buffer.Length + read > ContentPackageReader.MaxEntryBytes)
            {
                throw new ContentPackageException("Image source exceeds the size limit.");
            }

            buffer.Write(bytes, 0, read);
        }

        return ValidateSource(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), type);
    }

    public static ImageInfo ValidateSource(ReadOnlyMemory<byte> data, ContentPackageType type)
    {
        if (data.Length > ContentPackageReader.MaxEntryBytes)
        {
            throw new ContentPackageException("Image source exceeds the size limit.");
        }

        try
        {
            var format = ImageCodec.DetectFormat(data.Span);
            if (format is not (ImageFormat.Png or ImageFormat.WebP))
            {
                throw new ContentPackageException("Image source must be PNG or WebP.");
            }

            var info = ImageCodec.Identify(data.Span);
            var maximum = type switch
            {
                ContentPackageType.BlocksTexture => 8192,
                ContentPackageType.CharacterSkin => 1024,
                _ => throw new ContentPackageException("Image packages only support textures and skins.")
            };
            if (!ContentPayloadValidation.IsPowerOfTwo(info.Width) ||
                !ContentPayloadValidation.IsPowerOfTwo(info.Height) ||
                info.Width > maximum ||
                info.Height > maximum ||
                info.Frames != 1)
            {
                throw new ContentPackageException(
                    "Image dimensions or frame count are invalid for the selected content type."
                );
            }

            if (format == ImageFormat.Png)
            {
                if (!MemoryMarshal.TryGetArray(data, out var segment))
                {
                    segment = new ArraySegment<byte>(data.ToArray());
                }

                using var stream = new MemoryStream(segment.Array!, segment.Offset, segment.Count, false);
                PngPayloadValidator.Validate(stream, info.Width, info.Height, "image source");
            }
            else
            {
                WebPPayloadValidator.Validate(data.Span);
            }

            using var decoded = ImageCodec.Decode(data.Span, format);
            if (decoded.Width != info.Width || decoded.Height != info.Height)
            {
                throw new ContentPackageException("Decoded image does not match its header.");
            }

            return info;
        }
        catch (InvalidDataException exception)
        {
            throw new ContentPackageException("Image source must contain a valid static PNG or WebP.", exception);
        }
    }

    public void Validate(ContentPayloadValidationContext context)
    {
        var expectedFormat = GetImageFormat(context.Manifest.Payload.MediaType);
        var payload = CreatePayload(Type, expectedFormat);
        ContentPayloadValidation.ValidateEnvelope(
            context,
            payload.Format,
            payload.Entry,
            payload.MediaType,
            ["width", "height"]
        );
        var width = ContentPackageManifest.GetRequiredInt32(context.Manifest.Metadata, "width", "manifest.metadata");
        var height = ContentPackageManifest.GetRequiredInt32(context.Manifest.Metadata, "height", "manifest.metadata");
        using var stream = context.OpenEntry(payload.Entry);
        var info = ValidateSource(stream, Type);
        if (info.Format != expectedFormat || info.Width != width || info.Height != height)
        {
            throw new ContentPackageException(
                "Image encoding or dimensions do not match manifest.payload and metadata."
            );
        }
    }
}
