using System.Text.Json;

using Game.Content;

namespace Game.Managers;

public static class ContentInstallationManager
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static IReadOnlyList<ContentInstallation> Load()
    {
        if (!Storage.FileExists(GamePaths.InstalledContentFile))
        {
            return [];
        }

        using var stream = Storage.OpenFile(GamePaths.InstalledContentFile, OpenFileMode.Read);
        return JsonSerializer.Deserialize<List<ContentInstallation>>(stream, _jsonOptions) ?? [];
    }

    public static ContentInstallation Install(ContentCatalogItem item, byte[] package)
    {
        var type = Enum.Parse<ContentType>(item.Type, false);
        string installedName;
        if (type == ContentType.Mod)
        {
            var repository = new LocalModRepository(Storage.GetSystemPath(GamePaths.ModCache));
            installedName = repository.AddOrUpdatePackage(package, $"{item.PackageHash}.scpak").FileName;
        }
        else
        {
            using var stream = new MemoryStream(package, writable: false);
            installedName = ContentPackageManager.InstallPackage(stream, type, item.FileName);
        }

        var installations = Load().ToList();
        installations.RemoveAll(entry => entry.ContentId == item.ContentId && entry.VersionId == item.VersionId);
        var installation = new ContentInstallation(
            item.ContentId, item.VersionId, type, installedName, item.PackageHash, DateTimeOffset.UtcNow);
        installations.Add(installation);
        Save(installations);
        return installation;
    }

    public static bool Uninstall(ContentCatalogItem item)
    {
        var installations = Load().ToList();
        var installation = installations.FirstOrDefault(entry =>
            entry.ContentId == item.ContentId && entry.VersionId == item.VersionId);
        if (installation is null)
        {
            return false;
        }

        if (installation.Type == ContentType.Mod)
        {
            var repository = new LocalModRepository(Storage.GetSystemPath(GamePaths.ModCache));
            var package = repository.FindByHash(installation.PackageHash);
            if (package is not null)
            {
                repository.DeletePackage(package);
            }
        }
        else
        {
            ContentPackageManager.DeleteContent(installation.Type, installation.InstalledName);
        }

        installations.Remove(installation);
        Save(installations);
        return true;
    }

    private static void Save(IReadOnlyList<ContentInstallation> installations)
    {
        using var stream = Storage.OpenFile(GamePaths.InstalledContentFile, OpenFileMode.Create);
        JsonSerializer.Serialize(stream, installations, _jsonOptions);
    }
}

public sealed record ContentInstallation(
    string ContentId,
    string VersionId,
    ContentType Type,
    string InstalledName,
    string PackageHash,
    DateTimeOffset InstalledAt);
