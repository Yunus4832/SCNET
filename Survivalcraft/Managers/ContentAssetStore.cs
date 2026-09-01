using System.Text.Json;

namespace Game.Managers;

internal static class ContentAssetStore
{
    private sealed record Metadata(string AssetKey, string DisplayName);

    public static string Install(string directory, string extension, string displayName, Stream source,
        Action<Stream> validate)
    {
        Storage.CreateDirectory(directory);
        var assetKey = Guid.NewGuid().ToString("N");
        var name = assetKey + extension;
        var target = Storage.CombinePaths(directory, name);
        var metadataTarget = target + ".json";
        var temporaryData = Storage.CombinePaths(directory, $".{assetKey}.data.temp");
        var temporaryMetadata = Storage.CombinePaths(directory, $".{assetKey}.metadata.temp");
        try
        {
            using (var output = Storage.OpenFile(temporaryData, OpenFileMode.Create)) source.CopyTo(output);
            using (var input = Storage.OpenFile(temporaryData, OpenFileMode.Read)) validate(input);
            using (var output = Storage.OpenFile(temporaryMetadata, OpenFileMode.Create))
                JsonSerializer.Serialize(output, new Metadata(assetKey, displayName));
            Storage.MoveFile(temporaryData, target);
            Storage.MoveFile(temporaryMetadata, metadataTarget);
            return assetKey;
        }
        catch
        {
            DeleteIfExists(temporaryData);
            DeleteIfExists(temporaryMetadata);
            DeleteIfExists(target);
            DeleteIfExists(metadataTarget);
            throw;
        }
    }

    public static string Replace(string directory, string extension, string assetKey, string displayName,
        Stream source, Action<Stream> validate)
    {
        if (!Guid.TryParseExact(assetKey, "N", out _))
            throw new InvalidOperationException($"AssetKey '{assetKey}' is reserved or invalid.");
        var target = Storage.CombinePaths(directory, assetKey + extension);
        var metadataTarget = target + ".json";
        if (!Storage.FileExists(target) || !Storage.FileExists(metadataTarget))
            throw new InvalidOperationException($"Asset '{assetKey}' does not exist.");
        var temporaryData = Storage.CombinePaths(directory, $".{assetKey}.data.temp");
        var temporaryMetadata = Storage.CombinePaths(directory, $".{assetKey}.metadata.temp");
        var backupData = Storage.CombinePaths(directory, $".{assetKey}.data.backup");
        var backupMetadata = Storage.CombinePaths(directory, $".{assetKey}.metadata.backup");
        try
        {
            using (var output = Storage.OpenFile(temporaryData, OpenFileMode.Create)) source.CopyTo(output);
            using (var input = Storage.OpenFile(temporaryData, OpenFileMode.Read)) validate(input);
            using (var output = Storage.OpenFile(temporaryMetadata, OpenFileMode.Create))
                JsonSerializer.Serialize(output, new Metadata(assetKey, displayName));
            Storage.MoveFile(target, backupData);
            Storage.MoveFile(metadataTarget, backupMetadata);
            Storage.MoveFile(temporaryData, target);
            Storage.MoveFile(temporaryMetadata, metadataTarget);
            DeleteIfExists(backupData);
            DeleteIfExists(backupMetadata);
            return assetKey;
        }
        catch
        {
            DeleteIfExists(temporaryData);
            DeleteIfExists(temporaryMetadata);
            if (Storage.FileExists(backupData)) Storage.MoveFile(backupData, target);
            if (Storage.FileExists(backupMetadata)) Storage.MoveFile(backupMetadata, metadataTarget);
            throw;
        }
    }

    public static bool IsComplete(string directory, string name, string extension) =>
        Storage.GetExtension(name).Equals(extension, StringComparison.OrdinalIgnoreCase) &&
        Storage.FileExists(Storage.CombinePaths(directory, name + ".json"));

    public static string GetDisplayName(string directory, string assetKey, string extension)
    {
        using var input = Storage.OpenFile(Storage.CombinePaths(directory, assetKey + extension + ".json"), OpenFileMode.Read);
        return JsonSerializer.Deserialize<Metadata>(input)?.DisplayName
               ?? throw new InvalidOperationException($"Asset metadata for '{assetKey}' is invalid.");
    }

    public static void Delete(string directory, string assetKey, string extension)
    {
        DeleteIfExists(Storage.CombinePaths(directory, assetKey + extension));
        DeleteIfExists(Storage.CombinePaths(directory, assetKey + extension + ".json"));
    }

    private static void DeleteIfExists(string path)
    {
        if (Storage.FileExists(path)) Storage.DeleteFile(path);
    }
}
