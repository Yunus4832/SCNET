using System.Xml.Linq;

namespace Game.Modding;

public static class ModProfileManager
{
    public const string GlobalProfilePath = "config:ModProfile.xml";

    public static ModProfile LoadEffectiveProfile(string? sessionId)
    {
        if (TryLoadSessionProfile(sessionId, out var sessionProfile))
        {
            return Normalize(sessionProfile);
        }

        if (TryLoad(GlobalProfilePath, out var globalProfile))
        {
            return Normalize(globalProfile);
        }

        return CreateDefault("default");
    }

    public static void SaveSessionProfile(string sessionId, ModProfile profile)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        profile.Id = normalizedSessionId;
        Save(GetSessionProfilePath(normalizedSessionId), Normalize(profile));
    }

    public static void DeleteSessionProfile(string? sessionId)
    {
        var normalizedSessionId = NormalizeSessionId(sessionId);
        var path = GetSessionProfilePath(normalizedSessionId);
        if (Storage.FileExists(path))
        {
            Storage.DeleteFile(path);
        }
    }

    private static bool TryLoadSessionProfile(string? sessionId, out ModProfile profile)
    {
        return TryLoad(GetSessionProfilePath(NormalizeSessionId(sessionId)), out profile);
    }

    private static bool TryLoad(string path, out ModProfile profile)
    {
        profile = CreateDefault("default");
        try
        {
            if (!Storage.FileExists(path))
            {
                return false;
            }

            using var stream = Storage.OpenFile(path, OpenFileMode.Read);
            var root = XElement.Load(stream);
            profile = Parse(root);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load mod profile '{path}': {ex.Message}", ex);
        }
    }

    private static void Save(string path, ModProfile profile)
    {
        var directory = Storage.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Storage.DirectoryExists(directory))
        {
            Storage.CreateDirectory(directory);
        }

        using var stream = Storage.OpenFile(path, OpenFileMode.Create);
        var root = Serialize(Normalize(profile));
        root.Save(stream);
    }

    private static string GetSessionProfilePath(string sessionId)
    {
        return Storage.CombinePaths(GamePaths.Config, "SessionProfiles", $"{sessionId}.xml");
    }

    private static ModProfile Parse(XElement root)
    {
        var profile = new ModProfile
        {
            Id = root.Attribute(nameof(ModProfile.Id))?.Value ?? "default",
            RepositoryUrl = root.Attribute(nameof(ModProfile.RepositoryUrl))?.Value,
            Packages = root.Element(nameof(ModProfile.Packages))?
                .Elements("Package")
                .Select(element => new ModPackageRequirement
                {
                    ModId = element.Attribute(nameof(ModPackageRequirement.ModId))?.Value ?? string.Empty,
                    Version = element.Attribute(nameof(ModPackageRequirement.Version))?.Value ?? string.Empty,
                    PackageHash = element.Attribute(nameof(ModPackageRequirement.PackageHash))?.Value
                })
                .ToList() ?? []
        };
        return Normalize(profile);
    }

    private static XElement Serialize(ModProfile profile)
    {
        return new XElement("ModProfile",
            new XAttribute(nameof(ModProfile.Id), profile.Id),
            CreateOptionalAttribute(nameof(ModProfile.RepositoryUrl), profile.RepositoryUrl),
            new XElement(nameof(ModProfile.Packages),
                profile.Packages.Select(package => new XElement("Package",
                    new XAttribute(nameof(ModPackageRequirement.ModId), package.ModId),
                    new XAttribute(nameof(ModPackageRequirement.Version), package.Version),
                    CreateOptionalAttribute(nameof(ModPackageRequirement.PackageHash), package.PackageHash)))));
    }

    private static ModProfile Normalize(ModProfile profile)
    {
        profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? "default" : profile.Id.Trim();
        profile.RepositoryUrl = NormalizeRepositoryUrl(profile.RepositoryUrl);
        profile.Packages ??= [];
        profile.Packages = profile.Packages
            .Where(package => !string.IsNullOrWhiteSpace(package.ModId) && !string.IsNullOrWhiteSpace(package.Version))
            .Select(package => new ModPackageRequirement
            {
                ModId = package.ModId.Trim(),
                Version = package.Version.Trim(),
                PackageHash = string.IsNullOrWhiteSpace(package.PackageHash) ? null : package.PackageHash.Trim()
            })
            .ToList();
        return profile;
    }

    private static ModProfile CreateDefault(string id)
    {
        return new ModProfile
        {
            Id = id,
            Packages = []
        };
    }

    private static string NormalizeSessionId(string? sessionId)
    {
        return string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId.Trim();
    }

    private static string? NormalizeRepositoryUrl(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.TrimEnd('/');
    }

    private static object? CreateOptionalAttribute(string name, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : new XAttribute(name, value);
    }
}
