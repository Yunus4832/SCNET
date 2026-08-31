using SixLabors.ImageSharp;

namespace Content.Packaging.Payloads;

public sealed class BlocksTexturePayloadCodec : ImagePayloadCodec
{
    public override ContentPackageType Type => ContentPackageType.BlocksTexture;
    protected override string Format => "scnet.blocks-texture.png-v1";
    protected override string Entry => "payload/texture.png";
    protected override int MaximumDimension => 8192;
}

public sealed class CharacterSkinPayloadCodec : ImagePayloadCodec
{
    public override ContentPackageType Type => ContentPackageType.CharacterSkin;
    protected override string Format => "scnet.character-skin.png-v1";
    protected override string Entry => "payload/skin.png";
    protected override int MaximumDimension => 1024;
}

public abstract class ImagePayloadCodec : IContentPayloadCodec
{
    public abstract ContentPackageType Type { get; }
    protected abstract string Format { get; }
    protected abstract string Entry { get; }
    protected abstract int MaximumDimension { get; }

    public void Validate(ContentPayloadValidationContext context)
    {
        ContentPayloadValidation.ValidateEnvelope(context, Format, Entry, "image/png", ["width", "height"]);
        var width = ContentPackageManifest.GetRequiredInt32(context.Manifest.Metadata, "width", "manifest.metadata");
        var height = ContentPackageManifest.GetRequiredInt32(context.Manifest.Metadata, "height", "manifest.metadata");
        if (!ContentPayloadValidation.IsPowerOfTwo(width) || !ContentPayloadValidation.IsPowerOfTwo(height) ||
            width > MaximumDimension || height > MaximumDimension)
        {
            throw new ContentPackageException("Image metadata dimensions are invalid.");
        }

        using (var stream = context.OpenEntry(Entry))
        {
            PngPayloadValidator.Validate(stream, width, height, Entry);
        }
        try
        {
            using var stream = context.OpenEntry(Entry);
            using var image = Image.Load(stream);
            if (image.Width != width || image.Height != height || image.Frames.Count != 1)
            {
                throw new ContentPackageException("Decoded image does not match manifest.metadata.");
            }
        }
        catch (UnknownImageFormatException exception)
        {
            throw new ContentPackageException("Image payload cannot be decoded as PNG.", exception);
        }
        catch (InvalidImageContentException exception)
        {
            throw new ContentPackageException("Image payload contains invalid encoded data.", exception);
        }
        catch (ImageFormatException exception)
        {
            throw new ContentPackageException("Image payload contains invalid encoded data.", exception);
        }
    }
}
