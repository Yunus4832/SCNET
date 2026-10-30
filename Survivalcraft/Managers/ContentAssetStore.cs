using System.Text.Json;

using Engine.Media.Images;

namespace Game.Managers;

internal static class ContentAssetStore
{
    private sealed record Metadata(string AssetKey, string DisplayName);

    public static string Install(
        string directory,
        string? extension,
        string displayName,
        Stream source,
        Action<Stream> validate,
        string? assetKey = null
    )
    {
        Storage.CreateDirectory(directory);
        assetKey ??= Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(assetKey, "N", out _))
        {
            throw new InvalidOperationException($"AssetKey '{assetKey}' is reserved or invalid.");
        }

        string? target = null;
        string? metadataTarget = null;
        var temporaryData = Storage.CombinePaths(directory, $".{assetKey}.data.temp");
        var temporaryMetadata = Storage.CombinePaths(directory, $".{assetKey}.metadata.temp");
        var dataInstalled = false;
        var metadataInstalled = false;
        try
        {
            using (var output = Storage.OpenFile(temporaryData, OpenFileMode.Create))
            {
                source.CopyTo(output);
            }

            using (var input = Storage.OpenFile(temporaryData, OpenFileMode.Read))
            {
                validate(input);
                if (extension is null)
                {
                    input.Position = 0;
                    extension = ReadImageExtension(input);
                }
            }

            target = Storage.CombinePaths(directory, assetKey + extension);
            metadataTarget = target + ".json";
            using (var output = Storage.OpenFile(temporaryMetadata, OpenFileMode.Create))
            {
                JsonSerializer.Serialize(output, new Metadata(assetKey, displayName));
            }

            Storage.MoveFile(temporaryData, target);
            dataInstalled = true;
            Storage.MoveFile(temporaryMetadata, metadataTarget);
            metadataInstalled = true;
            return assetKey;
        }
        catch
        {
            DeleteIfExists(temporaryData);
            DeleteIfExists(temporaryMetadata);
            if (dataInstalled)
            {
                DeleteIfExists(target);
            }

            if (metadataInstalled)
            {
                DeleteIfExists(metadataTarget);
            }

            throw;
        }
    }

    public static string Replace(
        string directory,
        string? extension,
        string assetKey,
        string displayName,
        Stream source,
        Action<Stream> validate
    )
    {
        if (!Guid.TryParseExact(assetKey, "N", out _))
        {
            throw new InvalidOperationException($"AssetKey '{assetKey}' is reserved or invalid.");
        }

        var previousExtension = extension ?? FindImageExtension(directory, assetKey);
        var previousTarget = Storage.CombinePaths(directory, assetKey + previousExtension);
        var previousMetadata = previousTarget + ".json";
        var target = previousTarget;
        var metadataTarget = previousMetadata;
        if (!Storage.FileExists(previousTarget) || !Storage.FileExists(previousMetadata))
        {
            throw new InvalidOperationException($"Asset '{assetKey}' does not exist.");
        }

        var temporaryData = Storage.CombinePaths(directory, $".{assetKey}.data.temp");
        var temporaryMetadata = Storage.CombinePaths(directory, $".{assetKey}.metadata.temp");
        var backupData = Storage.CombinePaths(directory, $".{assetKey}.data.backup");
        var backupMetadata = Storage.CombinePaths(directory, $".{assetKey}.metadata.backup");
        var dataInstalled = false;
        var metadataInstalled = false;
        try
        {
            using (var output = Storage.OpenFile(temporaryData, OpenFileMode.Create))
            {
                source.CopyTo(output);
            }

            using (var input = Storage.OpenFile(temporaryData, OpenFileMode.Read))
            {
                validate(input);
                if (extension is null)
                {
                    input.Position = 0;
                    extension = ReadImageExtension(input);
                }
            }

            target = Storage.CombinePaths(directory, assetKey + extension);
            metadataTarget = target + ".json";
            using (var output = Storage.OpenFile(temporaryMetadata, OpenFileMode.Create))
            {
                JsonSerializer.Serialize(output, new Metadata(assetKey, displayName));
            }

            Storage.MoveFile(previousTarget, backupData);
            Storage.MoveFile(previousMetadata, backupMetadata);
            Storage.MoveFile(temporaryData, target);
            dataInstalled = true;
            Storage.MoveFile(temporaryMetadata, metadataTarget);
            metadataInstalled = true;
            DeleteIfExists(backupData);
            DeleteIfExists(backupMetadata);
            return assetKey;
        }
        catch
        {
            DeleteIfExists(temporaryData);
            DeleteIfExists(temporaryMetadata);
            if (dataInstalled)
            {
                DeleteIfExists(target);
            }

            if (metadataInstalled)
            {
                DeleteIfExists(metadataTarget);
            }

            if (Storage.FileExists(backupData))
            {
                Storage.MoveFile(backupData, previousTarget);
            }

            if (Storage.FileExists(backupMetadata))
            {
                Storage.MoveFile(backupMetadata, previousMetadata);
            }

            throw;
        }
    }

    public static bool IsComplete(string directory, string name, string extension) =>
        Storage.GetExtension(name).Equals(extension, StringComparison.OrdinalIgnoreCase) &&
        Storage.FileExists(Storage.CombinePaths(directory, name + ".json"));

    public static string GetDisplayName(string directory, string assetKey, string extension)
    {
        using var input = Storage.OpenFile(
            Storage.CombinePaths(directory, assetKey + extension + ".json"),
            OpenFileMode.Read
        );
        return JsonSerializer.Deserialize<Metadata>(input)?.DisplayName ??
               throw new InvalidOperationException($"Asset metadata for '{assetKey}' is invalid.");
    }

    public static void Delete(string directory, string assetKey, string extension)
    {
        DeleteIfExists(Storage.CombinePaths(directory, assetKey + extension));
        DeleteIfExists(Storage.CombinePaths(directory, assetKey + extension + ".json"));
    }

    public static string? FindImageExtension(string directory, string assetKey) =>
        new[] { ".png", ".webp" }.SingleOrDefault(extension =>
            Storage.FileExists(Storage.CombinePaths(directory, assetKey + extension)) &&
            IsComplete(directory, assetKey + extension, extension)
        );

    public static bool IsImageComplete(string directory, string name) =>
        (Storage.GetExtension(name) is ".png" or ".webp") &&
        Storage.FileExists(Storage.CombinePaths(directory, name + ".json"));

    private static string ReadImageExtension(Stream input)
    {
        Span<byte> header = stackalloc byte[12];
        var count = input.ReadAtLeast(header, header.Length, false);
        return ImageCodec.DetectFormat(header[..count]) switch
        {
            ImageFormat.Png => ".png",
            ImageFormat.WebP => ".webp",
            _ => throw new InvalidDataException("Image assets must be PNG or WebP.")
        };
    }

    private static void DeleteIfExists(string? path)
    {
        if (path is not null && Storage.FileExists(path))
        {
            Storage.DeleteFile(path);
        }
    }
}
