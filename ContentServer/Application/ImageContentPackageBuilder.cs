using System.Security.Cryptography;
using System.Text.Json;

using Content.Packaging;
using ContentServer.Infrastructure;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace ContentServer.Application;

public sealed record ImageSourceInspection(int Width, int Height, long Size, string Sha256, string MediaType);

public sealed class ImageContentPackageBuilder(ContentPackageStore packageStore)
{
    public async Task<ImageSourceInspection> ValidateSourceAsync(
        Stream source,
        ContentPackageType type,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        var path = packageStore.CreateTemporaryPath(".png-source");
        try
        {
            await CopySourceAsync(source, path, maximumBytes, cancellationToken);
            var inspection = await InspectSourceAsync(path, cancellationToken);
            ValidateDimensions(type, inspection.Width, inspection.Height);
            return inspection;
        }
        finally
        {
            File.Delete(path);
        }
    }

    public async Task<StagedContentPackage> BuildAsync(
        Stream source,
        ContentPackageType type,
        string identifier,
        string name,
        string version,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (type is not (ContentPackageType.BlocksTexture or ContentPackageType.CharacterSkin))
            throw new ContentPackageException("Web image manufacturing only supports texture and skin packages.");

        var sourcePath = packageStore.CreateTemporaryPath(".png-source");
        var packagePath = packageStore.CreateTemporaryPath(ContentPackageReader.FileExtension);
        try
        {
            await CopySourceAsync(source, sourcePath, maximumBytes, cancellationToken);
            var sourceInspection = await InspectSourceAsync(sourcePath, cancellationToken);
            ValidateDimensions(type, sourceInspection.Width, sourceInspection.Height);
            var payloadPath = type == ContentPackageType.BlocksTexture
                ? "payload/texture.png"
                : "payload/skin.png";
            var payload = type == ContentPackageType.BlocksTexture
                ? new ContentPackagePayload("scnet.blocks-texture.png-v1", payloadPath, "image/png")
                : new ContentPackagePayload("scnet.character-skin.png-v1", payloadPath, "image/png");
            var metadata = JsonSerializer.SerializeToElement(new
            {
                width = sourceInspection.Width,
                height = sourceInspection.Height
            });
            var manifest = new ContentPackageManifest(ContentPackageManifest.CurrentFormatVersion,
                type, identifier, name, version, payload, metadata);
            await using (var output = new FileStream(packagePath, FileMode.CreateNew, FileAccess.ReadWrite,
                             FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                ContentPackageWriter.Write(output, manifest,
                [
                    new ContentPackageWriteEntry(payloadPath, sourceInspection.Size,
                        () => File.OpenRead(sourcePath))
                ]);
                await output.FlushAsync(cancellationToken);
            }
            return await packageStore.InspectTemporaryPackageAsync(packagePath,
                $"{identifier}-{version}{ContentPackageReader.FileExtension}", cancellationToken);
        }
        catch
        {
            File.Delete(packagePath);
            throw;
        }
        finally
        {
            File.Delete(sourcePath);
        }
    }

    private static async Task CopySourceAsync(Stream source, string path, long maximumBytes,
        CancellationToken cancellationToken)
    {
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maximumBytes) throw new ContentPackageException("PNG source exceeds the size limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        await output.FlushAsync(cancellationToken);
    }

    private static async Task<ImageSourceInspection> InspectSourceAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(path);
        var imageInfo = await Image.IdentifyAsync(input, cancellationToken);
        if (imageInfo.Metadata.DecodedImageFormat is not PngFormat)
            throw new ContentPackageException("Image source must be PNG.");
        input.Position = 0;
        using (var image = await Image.LoadAsync(input, cancellationToken))
        {
            if (image.Frames.Count != 1)
                throw new ContentPackageException("PNG source must contain exactly one frame.");
        }
        input.Position = 0;
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
        return new ImageSourceInspection(imageInfo.Width, imageInfo.Height, input.Length, hash, "image/png");
    }

    private static void ValidateDimensions(ContentPackageType type, int width, int height)
    {
        var maximum = type switch
        {
            ContentPackageType.BlocksTexture => 8192,
            ContentPackageType.CharacterSkin => 1024,
            _ => throw new ContentPackageException("Web image manufacturing only supports texture and skin packages.")
        };
        if (width <= 0 || height <= 0 || (width & (width - 1)) != 0 || (height & (height - 1)) != 0 ||
            width > maximum || height > maximum)
            throw new ContentPackageException("PNG source dimensions are invalid for the selected content type.");
    }
}
