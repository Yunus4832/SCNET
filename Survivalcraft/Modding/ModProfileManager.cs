using System.Xml.Linq;

namespace Game.Modding;

public static class ModProfileManager
{
    public static string GlobalProfilePath => GamePaths.GlobalModProfileFile;

    public static string EmptyDataHash { get; } = ComputeDataHash(null);

    public static string ComputeDataHash(ModProfile? profile)
    {
        var lines = (profile?.Packages ?? [])
            .Select(CreateDataHashLine)
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase);
        return HashUtils.ComputeSha256(string.Join('\n', lines));
    }

    public static ModProfile LoadEffectiveProfile(string? sessionId)
    {
        return LoadEffectiveProfile(sessionId, null);
    }

    public static ModProfile LoadEffectiveProfile(string? sessionId, SessionInfo? sessionInfo)
    {
        if (TryLoadSessionProfile(sessionId, out var sessionProfile))
        {
            return sessionProfile;
        }

        return ResolveProfileForSessionTarget(sessionInfo);
    }

    public static ModProfile LoadGlobalProfile()
    {
        return TryLoadGlobalProfile(out var profile) ? profile : CreateDefault("default");
    }

    public static ModProfile ResolveProfileForSessionTarget(SessionInfo? sessionInfo)
    {
        var globalProfile = LoadGlobalProfile();
        if (TryResolveWorldProfileContext(sessionInfo, out var worldContext))
        {
            return ResolveWorldEffectiveProfile(
                globalProfile,
                worldContext.WorldDirectoryName,
                worldContext.WorldName,
                worldContext.Strategy);
        }

        return globalProfile;
    }

    public static ModProfile? LoadSessionProfile(string? sessionId)
    {
        return TryLoadSessionProfile(sessionId, out var profile) ? profile : null;
    }

    public static ModProfile? LoadWorldProfile(string? worldDirectoryName)
    {
        return TryLoadWorldProfile(worldDirectoryName, out var profile) ? profile : null;
    }

    public static void SaveSessionProfile(ModProfile profile)
    {
        profile = Normalize(profile);
        if (string.IsNullOrWhiteSpace(profile.Id) || string.Equals(profile.Id, "default", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Session profile id is required.");
        }

        SaveProfile(GetSessionProfilePath(profile.Id), profile);
    }

    public static void SaveWorldProfile(string worldDirectoryName, ModProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectoryName);
        ArgumentNullException.ThrowIfNull(profile);
        var normalized = Normalize(profile);
        normalized.Id = GetWorldProfileId(worldDirectoryName);
        SaveProfile(GetWorldProfilePath(worldDirectoryName), normalized);
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

    public static void DeleteWorldProfile(string? worldDirectoryName)
    {
        if (string.IsNullOrWhiteSpace(worldDirectoryName))
        {
            return;
        }

        var path = GetWorldProfilePath(worldDirectoryName);
        if (Storage.FileExists(path))
        {
            Storage.DeleteFile(path);
        }
    }

    public static ModProfile CreateSessionProfile(string sessionId, ModProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return Normalize(new ModProfile
        {
            Id = NormalizeSessionId(sessionId),
            RepositoryUrl = profile.RepositoryUrl,
            Packages = profile.Packages
                .Select(package => new ModPackageRequirement
                {
                    ModId = package.ModId,
                    Version = package.Version,
                    PackageHash = package.PackageHash
                })
                .ToList()
        });
    }

    private static bool TryLoadGlobalProfile(out ModProfile profile)
    {
        return TryLoadGlobalProfile(GlobalProfilePath, out profile);
    }

    private static bool TryLoadGlobalProfile(string path, out ModProfile profile)
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
            profile = ParseModProfile(root);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load mod profile '{path}': {ex.Message}", ex);
        }
    }

    private static bool TryLoadSessionProfile(string? sessionId, out ModProfile profile)
    {
        profile = CreateDefault(NormalizeSessionId(sessionId));
        try
        {
            var path = GetSessionProfilePath(NormalizeSessionId(sessionId));
            if (!Storage.FileExists(path))
            {
                return false;
            }

            using var stream = Storage.OpenFile(path, OpenFileMode.Read);
            var root = XElement.Load(stream);
            profile = ParseModProfile(root);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to load session profile '{GetSessionProfilePath(NormalizeSessionId(sessionId))}': {ex.Message}",
                ex);
        }
    }

    private static bool TryLoadWorldProfile(string? worldDirectoryName, out ModProfile profile)
    {
        profile = CreateDefault("default");
        if (string.IsNullOrWhiteSpace(worldDirectoryName))
        {
            return false;
        }

        return TryLoadGlobalProfile(GetWorldProfilePath(worldDirectoryName), out profile);
    }

    private static void SaveProfile(string path, ModProfile profile)
    {
        var directory = Storage.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Storage.DirectoryExists(directory))
        {
            Storage.CreateDirectory(directory);
        }

        using var stream = Storage.OpenFile(path, OpenFileMode.Create);
        var root = SerializeModProfile(Normalize(profile));
        root.Save(stream);
    }

    private static string GetSessionProfilePath(string sessionId)
    {
        return Storage.CombinePaths(GamePaths.SessionProfilesDirectory, $"{sessionId}.xml");
    }

    public static string GetWorldProfilePath(string worldDirectoryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(worldDirectoryName);
        return Storage.CombinePaths(worldDirectoryName, "WorldModProfile.xml");
    }

    private static ModProfile ParseModProfile(XElement root)
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

    private static XElement SerializeModProfile(ModProfile profile)
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

    private static bool TryResolveWorldProfileContext(SessionInfo? sessionInfo, out WorldProfileContext context)
    {
        context = default;
        if (sessionInfo == null || sessionInfo.Target != SessionTarget.World || string.IsNullOrWhiteSpace(sessionInfo.World))
        {
            return false;
        }

        WorldsManager.UpdateWorldsList();
        var normalizedWorld = sessionInfo.World.Trim();
        var worldInfo = WorldsManager.WorldInfos.FirstOrDefault(world =>
                            string.Equals(world.DirectoryName, normalizedWorld, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(world.WorldSettings.Name, normalizedWorld, StringComparison.OrdinalIgnoreCase))
                        ?? null;
        if (worldInfo == null)
        {
            var worldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, normalizedWorld);
            if (Storage.DirectoryExists(worldDirectoryName))
            {
                worldInfo = WorldsManager.GetWorldInfo(worldDirectoryName);
            }
        }

        if (worldInfo == null)
        {
            return false;
        }

        context = new WorldProfileContext(
            worldInfo.DirectoryName,
            worldInfo.WorldSettings.Name,
            worldInfo.WorldSettings.ModProfileResolutionStrategy);
        return true;
    }

    private static ModProfile ResolveWorldEffectiveProfile(
        ModProfile globalProfile,
        string worldDirectoryName,
        string worldName,
        ModProfileResolutionStrategy strategy)
    {
        var hasWorldProfile = TryLoadWorldProfile(worldDirectoryName, out var worldProfile);
        return strategy switch
        {
            ModProfileResolutionStrategy.WorldOnly => hasWorldProfile
                ? CloneProfile(worldProfile!, GetWorldProfileId(worldDirectoryName))
                : CreateDefault(GetWorldProfileId(worldDirectoryName)),
            ModProfileResolutionStrategy.GlobalPlusWorld => hasWorldProfile
                ? MergeProfiles(
                    globalProfile,
                    worldProfile!,
                    worldName,
                    repositoryUrl: worldProfile!.RepositoryUrl ?? globalProfile.RepositoryUrl,
                    worldOverrides: true)
                : CloneProfile(globalProfile, worldName),
            ModProfileResolutionStrategy.WorldPlusGlobal => hasWorldProfile
                ? MergeProfiles(
                    worldProfile!,
                    globalProfile,
                    worldName,
                    repositoryUrl: worldProfile!.RepositoryUrl ?? globalProfile.RepositoryUrl,
                    worldOverrides: false)
                : CloneProfile(globalProfile, worldName),
            _ => CloneProfile(globalProfile, worldName)
        };
    }

    private static ModProfile MergeProfiles(
        ModProfile primary,
        ModProfile secondary,
        string resultId,
        string? repositoryUrl,
        bool worldOverrides)
    {
        var packages = new Dictionary<string, ModPackageRequirement>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in primary.Packages)
        {
            packages[package.ModId] = CloneRequirement(package);
        }

        foreach (var package in secondary.Packages)
        {
            if (worldOverrides || !packages.ContainsKey(package.ModId))
            {
                packages[package.ModId] = CloneRequirement(package);
            }
        }

        return Normalize(new ModProfile
        {
            Id = string.IsNullOrWhiteSpace(resultId) ? "default" : resultId.Trim(),
            RepositoryUrl = repositoryUrl,
            Packages = packages.Values
                .OrderBy(package => package.ModId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
                .ToList()
        });
    }

    private static ModProfile CloneProfile(ModProfile profile, string id)
    {
        profile = Normalize(profile);
        return new ModProfile
        {
            Id = string.IsNullOrWhiteSpace(id) ? profile.Id : id.Trim(),
            RepositoryUrl = profile.RepositoryUrl,
            Packages = profile.Packages.Select(CloneRequirement).ToList()
        };
    }

    private static ModPackageRequirement CloneRequirement(ModPackageRequirement package)
    {
        return new ModPackageRequirement
        {
            ModId = package.ModId,
            Version = package.Version,
            PackageHash = package.PackageHash
        };
    }

    private static string CreateDataHashLine(ModPackageRequirement package)
    {
        return $"{package.ModId.Trim()}|{package.Version.Trim()}|{package.PackageHash?.Trim() ?? string.Empty}";
    }

    private static string GetWorldProfileId(string worldDirectoryName)
    {
        return Storage.GetFileName(worldDirectoryName);
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

    private readonly record struct WorldProfileContext(
        string WorldDirectoryName,
        string WorldName,
        ModProfileResolutionStrategy Strategy);
}
