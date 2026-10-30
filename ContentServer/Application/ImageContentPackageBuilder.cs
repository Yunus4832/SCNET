using System.Security.Cryptography;
using System.Text.Json;

using Content.Packaging;
using Content.Packaging.Payloads;

using ContentServer.Infrastructure;

namespace ContentServer.Application;

public sealed record ImageSourceInspection(int Width, int Height, long Size, string Sha256, string MediaType);

public sealed class ImageContentPackageBuilder(ContentPackageStore packageStore)
{
    public async Task<ImageSourceInspection> ValidateSourceAsync(
        Stream source,
        ContentPackageType type,
        long maximumBytes,
        CancellationToken cancellationToken
    )
    {
        var path = packageStore.CreateTemporaryPath(".image-source");
        try
        {
            await CopySourceAsync(source, path, maximumBytes, cancellationToken);
            var inspection = await InspectSourceAsync(path, type, cancellationToken);
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
        string summary,
        string description,
        string version,
        long maximumBytes,
        CancellationToken cancellationToken
    )
    {
        if (type is not (ContentPackageType.BlocksTexture or ContentPackageType.CharacterSkin))
        {
            throw new ContentPackageException("Web image manufacturing only supports texture and skin packages.");
        }

        var sourcePath = packageStore.CreateTemporaryPath(".image-source");
        var packagePath = packageStore.CreateTemporaryPath(ContentPackageReader.FileExtension);
        try
        {
            await CopySourceAsync(source, sourcePath, maximumBytes, cancellationToken);
            var sourceInspection = await InspectSourceAsync(sourcePath, type, cancellationToken);
            var payload = ImagePayloadCodec.CreatePayload(
                type,
                ImagePayloadCodec.GetImageFormat(sourceInspection.MediaType)
            );
            var payloadPath = payload.Entry;
            var metadata = JsonSerializer.SerializeToElement(
                new
                {
                    width = sourceInspection.Width,
                    height = sourceInspection.Height
                }
            );
            var manifest = new ContentPackageManifest(
                ContentPackageManifest.CurrentFormatVersion,
                type,
                identifier,
                name,
                summary,
                description,
                version,
                payload,
                metadata
            );
            await using (var output = new FileStream(
                             packagePath,
                             FileMode.CreateNew,
                             FileAccess.ReadWrite,
                             FileShare.None,
                             64 * 1024,
                             FileOptions.Asynchronous | FileOptions.SequentialScan
                         ))
            {
                ContentPackageWriter.Write(
                    output,
                    manifest,
                    [
                        new ContentPackageWriteEntry(
                            payloadPath,
                            sourceInspection.Size,
                            () => File.OpenRead(sourcePath)
                        )
                    ]
                );
                await output.FlushAsync(cancellationToken);
            }

            return await packageStore.InspectTemporaryPackageAsync(
                packagePath,
                $"{identifier}-{version}{ContentPackageReader.FileExtension}",
                cancellationToken
            );
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

    private static async Task CopySourceAsync(
        Stream source,
        string path,
        long maximumBytes,
        CancellationToken cancellationToken
    )
    {
        await using var output = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan
        );
        var buffer = new byte[64 * 1024];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > Math.Min(maximumBytes, ContentPackageReader.MaxEntryBytes))
            {
                throw new ContentPackageException("Image source exceeds the size limit.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        await output.FlushAsync(cancellationToken);
    }

    private static async Task<ImageSourceInspection> InspectSourceAsync(
        string path,
        ContentPackageType type,
        CancellationToken cancellationToken
    )
    {
        var data = await File.ReadAllBytesAsync(path, cancellationToken);
        var info = ImagePayloadCodec.ValidateSource(data, type);
        cancellationToken.ThrowIfCancellationRequested();
        var hash = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        var mediaType = ImagePayloadCodec.CreatePayload(type, info.Format).MediaType;
        return new ImageSourceInspection(info.Width, info.Height, data.Length, hash, mediaType);
    }
}
