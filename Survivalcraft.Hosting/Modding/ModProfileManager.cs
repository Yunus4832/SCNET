using System.Xml.Linq;

using Game.FileStorage;
using Game.Startup;

namespace Game.Modding;

public static class ModProfileManager
{
    private static WorldRepository? _worlds;

    public static void Initialize(WorldRepository worlds)
    {
        _worlds = worlds ?? throw new ArgumentNullException(nameof(worlds));
    }

    private static WorldRepository Worlds => _worlds ??
                                             throw new InvalidOperationException(
                                                 "Initialize profile storage with the instance world repository."
                                             );

    public static string GlobalProfilePath => GamePaths.GlobalModProfileFile;

    public static string EmptyDataHash => GameProtocol.EmptyModDataHash;

    /// <summary>按启动目标选择完整配置，已有世界以存档为准，联机重启使用已准备的会话配置。</summary>
    public static ModProfile ResolveStartupProfile(SessionInfo session)
    {
        if (session.Target is SessionTarget.World or SessionTarget.HostedWorld)
        {
            if (TryResolveWorldDirectory(session, out var directory)) { return LoadRequiredWorldProfile(directory); }

            if (session.WorldCreation is not null)
            {
                return LoadSessionProfile(session.SessionId) ??
                       throw new InvalidOperationException("Pending world creation profile is missing.");
            }
        }

        return LoadSessionProfile(session.SessionId) ?? LoadGlobalProfile();
    }

    public static ModProfile LoadGlobalProfile()
    {
        return TryLoadGlobalProfile(out var profile) ? profile : CreateDefault("default");
    }

    public static void SaveGlobalProfile(ModProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var normalized = Normalize(profile);
        normalized.Id = "default";
        SaveProfile(GlobalProfilePath, normalized);
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
        return Normalize(
            new ModProfile
            {
                Id = NormalizeSessionId(sessionId),
                Packages = profile.Packages
                    .Select(package => new ModPackageRequirement
                        {
                            ModId = package.ModId,
                            Version = package.Version,
                            PackageHash = package.PackageHash
                        }
                    )
                    .ToList()
            }
        );
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
                ex
            );
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
            Packages = root.Element(nameof(ModProfile.Packages))?
                           .Elements("Package")
                           .Select(element => new ModPackageRequirement
                               {
                                   ModId =
                                       element.Attribute(nameof(ModPackageRequirement.ModId))?.Value ?? string.Empty,
                                   Version = element.Attribute(nameof(ModPackageRequirement.Version))?.Value ??
                                             string.Empty,
                                   PackageHash = element.Attribute(nameof(ModPackageRequirement.PackageHash))?.Value ??
                                                 string.Empty
                               }
                           )
                           .ToList() ??
                       []
        };
        return Normalize(profile);
    }

    private static XElement SerializeModProfile(ModProfile profile)
    {
        return new XElement(
            "ModProfile",
            new XAttribute(nameof(ModProfile.Id), profile.Id),
            new XElement(
                nameof(ModProfile.Packages),
                profile.Packages.Select(package => new XElement(
                        "Package",
                        new XAttribute(nameof(ModPackageRequirement.ModId), package.ModId),
                        new XAttribute(nameof(ModPackageRequirement.Version), package.Version),
                        new XAttribute(nameof(ModPackageRequirement.PackageHash), package.PackageHash)
                    )
                )
            )
        );
    }

    private static ModProfile Normalize(ModProfile profile)
    {
        profile.Id = string.IsNullOrWhiteSpace(profile.Id) ? "default" : profile.Id.Trim();
        profile.Packages ??= [];
        profile.Packages = profile.Packages
            .Select(package => new ModPackageRequirement
                {
                    ModId = package.ModId.Trim(),
                    Version = package.Version.Trim(),
                    PackageHash = package.PackageHash.Trim()
                }
            )
            .ToList();
        ModProfileValidation.Validate(profile);
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

    private static bool TryResolveWorldDirectory(SessionInfo? sessionInfo, out string worldDirectoryName)
    {
        worldDirectoryName = string.Empty;
        if (sessionInfo == null ||
            sessionInfo.Target is not (SessionTarget.World or SessionTarget.HostedWorld) ||
            string.IsNullOrWhiteSpace(sessionInfo.World))
        {
            return false;
        }

        Worlds.UpdateWorldsList();
        var normalizedWorld = sessionInfo.World.Trim();
        var worldInfo = Worlds.WorldInfos.FirstOrDefault(world =>
            string.Equals(world.DirectoryName, normalizedWorld, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                world.WorldSettings.Name,
                normalizedWorld,
                StringComparison.OrdinalIgnoreCase
            )
        );
        if (worldInfo == null)
        {
            worldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, normalizedWorld);
            if (Storage.DirectoryExists(worldDirectoryName))
            {
                return true;
            }
        }

        if (worldInfo == null)
        {
            return false;
        }

        worldDirectoryName = worldInfo.DirectoryName;
        return true;
    }

    public static ModProfile LoadRequiredWorldProfile(string worldDirectoryName)
    {
        return LoadWorldProfile(worldDirectoryName) ??
               throw new InvalidOperationException(
                   $"Existing world is missing its complete mod profile '{GetWorldProfilePath(worldDirectoryName)}'."
               );
    }

    private static string GetWorldProfileId(string worldDirectoryName)
    {
        return Storage.GetFileName(worldDirectoryName);
    }

    private static string NormalizeSessionId(string? sessionId)
    {
        return string.IsNullOrWhiteSpace(sessionId) ? "default" : sessionId.Trim();
    }
}
