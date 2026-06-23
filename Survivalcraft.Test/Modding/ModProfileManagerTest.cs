using System.Xml.Linq;

using Engine.FileStorage;

using EntitySystem.TemplatesDatabase;

using Game;
using Game.Managers;
using Game.Modding;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class ModProfileManagerTest : IDisposable
{
    private readonly FileBackup _globalProfileBackup = FileBackup.Create(ModProfileManager.GlobalProfilePath);
    private readonly DirectoryBackup _sessionProfilesBackup =
        DirectoryBackup.Create(Storage.CombinePaths(GamePaths.Config, "SessionProfiles"));
    private readonly DirectoryBackup _worldsBackup =
        DirectoryBackup.Create(GamePaths.Worlds);

    [Fact]
    public void LoadEffectiveProfilePrefersSessionProfile()
    {
        SaveGlobalProfile("""
            <ModProfile Id="global" RepositoryUrl="https://global.example">
              <Packages>
                <Package ModId="global.mod" Version="1.0.0" />
              </Packages>
            </ModProfile>
            """);
        ModProfileManager.SaveSessionProfile(new ModProfile
        {
            Id = "session-a",
            RepositoryUrl = "https://session.example/",
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "session.mod",
                    Version = "2.0.0",
                    PackageHash = "abc"
                }
            ]
        });

        var profile = ModProfileManager.LoadEffectiveProfile("session-a");

        Assert.Equal("session-a", profile.Id);
        Assert.Equal("https://session.example", profile.RepositoryUrl);
        var package = Assert.Single(profile.Packages);
        Assert.Equal("session.mod", package.ModId);
        Assert.Equal("2.0.0", package.Version);
        Assert.Equal("abc", package.PackageHash);
    }

    [Fact]
    public void RemoteServerSessionProfileDoesNotMergeGlobalProfile()
    {
        SaveGlobalProfile("""
            <ModProfile Id="global" RepositoryUrl="https://global.example">
              <Packages>
                <Package ModId="global.mod" Version="1.0.0" />
              </Packages>
            </ModProfile>
            """);
        ModProfileManager.SaveSessionProfile(new ModProfile
        {
            Id = "remote-session",
            RepositoryUrl = "https://server.example",
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "server.mod",
                    Version = "2.0.0",
                    PackageHash = "server-hash"
                }
            ]
        });

        var profile = ModProfileManager.LoadEffectiveProfile("remote-session", new SessionInfo
        {
            Target = SessionTarget.RemoteServer,
            ServerHost = "127.0.0.1",
            ServerPort = 28887
        });

        Assert.Equal("remote-session", profile.Id);
        Assert.Equal("https://server.example", profile.RepositoryUrl);
        var package = Assert.Single(profile.Packages);
        Assert.Equal("server.mod", package.ModId);
        Assert.Equal("2.0.0", package.Version);
        Assert.Equal("server-hash", package.PackageHash);
    }

    [Fact]
    public void LoadSessionProfileReturnsModProfile()
    {
        ModProfileManager.SaveSessionProfile(new ModProfile
        {
            Id = "session-b",
            RepositoryUrl = "https://session.example/",
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "session.mod",
                    Version = "2.0.0",
                    PackageHash = "abc"
                }
            ]
        });

        var profile = ModProfileManager.LoadSessionProfile("session-b");

        Assert.NotNull(profile);
        Assert.Equal("session-b", profile!.Id);
        Assert.Equal("https://session.example", profile.RepositoryUrl);
        Assert.Equal("session.mod", Assert.Single(profile.Packages).ModId);
    }

    [Fact]
    public void LoadEffectiveProfileFallsBackToDefaultWhenNoProfileExists()
    {
        DeleteIfExists(ModProfileManager.GlobalProfilePath);
        DeleteDirectoryIfExists(Storage.CombinePaths(GamePaths.Config, "SessionProfiles"));

        var profile = ModProfileManager.LoadEffectiveProfile("missing");

        Assert.Equal("default", profile.Id);
        Assert.Null(profile.RepositoryUrl);
        Assert.Empty(profile.Packages);
    }

    [Fact]
    public void SaveWorldProfileUsesWorldDirectorySidecar()
    {
        var worldDirectoryName = CreateWorldDirectory("WorldSidecar", ModProfileResolutionStrategy.WorldOnly);
        ModProfileManager.SaveWorldProfile(worldDirectoryName, new ModProfile
        {
            Id = "ignored",
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "world.mod",
                    Version = "1.0.0"
                }
            ]
        });

        var path = ModProfileManager.GetWorldProfilePath(worldDirectoryName);
        using var stream = Storage.OpenFile(path, OpenFileMode.Read);
        var root = XElement.Load(stream);

        Assert.Equal("ModProfile", root.Name.LocalName);
        Assert.Equal(Path.GetFileName(worldDirectoryName), root.Attribute(nameof(ModProfile.Id))?.Value);
    }

    [Fact]
    public void LoadEffectiveProfileUsesWorldOnlyStrategyWhenNoSessionProfileExists()
    {
        DeleteIfExists(ModProfileManager.GlobalProfilePath);
        var worldDirectoryName = CreateWorldDirectory("WorldOnly", ModProfileResolutionStrategy.WorldOnly);
        ModProfileManager.SaveWorldProfile(worldDirectoryName, new ModProfile
        {
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "world.mod",
                    Version = "2.0.0"
                }
            ]
        });

        var profile = ModProfileManager.LoadEffectiveProfile("missing-session", new SessionInfo
        {
            Target = SessionTarget.World,
            World = "WorldOnly"
        });

        var package = Assert.Single(profile.Packages);
        Assert.Equal("world.mod", package.ModId);
        Assert.Equal("2.0.0", package.Version);
    }

    [Fact]
    public void NewWorldDefaultsToGlobalPlusWorldStrategy()
    {
        var worldSettings = new WorldSettings();

        Assert.Equal(ModProfileResolutionStrategy.GlobalPlusWorld, worldSettings.ModProfileResolutionStrategy);
    }

    [Fact]
    public void LoadEffectiveProfileResolvesWorldProfileByWorldNameNotOnlyDirectoryName()
    {
        DeleteIfExists(ModProfileManager.GlobalProfilePath);
        var worldDirectoryName = CreateWorldDirectory(
            "VisibleWorldName",
            ModProfileResolutionStrategy.WorldOnly,
            directoryName: "HiddenDirectoryName");
        ModProfileManager.SaveWorldProfile(worldDirectoryName, new ModProfile
        {
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "world.mod",
                    Version = "5.0.0"
                }
            ]
        });

        var profile = ModProfileManager.LoadEffectiveProfile("missing-session", new SessionInfo
        {
            Target = SessionTarget.World,
            World = "VisibleWorldName"
        });

        var package = Assert.Single(profile.Packages);
        Assert.Equal("world.mod", package.ModId);
        Assert.Equal("5.0.0", package.Version);
    }

    [Fact]
    public void LoadEffectiveProfileMergesGlobalAndWorldProfileAccordingToStrategy()
    {
        SaveGlobalProfile("""
            <ModProfile Id="global" RepositoryUrl="https://global.example">
              <Packages>
                <Package ModId="shared.mod" Version="1.0.0" />
                <Package ModId="global.mod" Version="1.0.0" />
              </Packages>
            </ModProfile>
            """);
        var worldDirectoryName = CreateWorldDirectory("WorldMerge", ModProfileResolutionStrategy.GlobalPlusWorld);
        ModProfileManager.SaveWorldProfile(worldDirectoryName, new ModProfile
        {
            RepositoryUrl = "https://world.example/",
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "shared.mod",
                    Version = "2.0.0"
                },
                new ModPackageRequirement
                {
                    ModId = "world.mod",
                    Version = "1.0.0"
                }
            ]
        });

        var profile = ModProfileManager.LoadEffectiveProfile("missing-session", new SessionInfo
        {
            Target = SessionTarget.World,
            World = "WorldMerge"
        });

        Assert.Equal("https://world.example", profile.RepositoryUrl);
        Assert.Equal(
            ["global.mod:1.0.0", "shared.mod:2.0.0", "world.mod:1.0.0"],
            profile.Packages.Select(package => $"{package.ModId}:{package.Version}").OrderBy(x => x));
    }

    [Fact]
    public void SaveSessionProfileUsesDedicatedSessionRoot()
    {
        ModProfileManager.SaveSessionProfile(new ModProfile
        {
            Id = "session-c",
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "session.mod",
                    Version = "3.0.0"
                }
            ]
        });

        var path = Storage.CombinePaths(GamePaths.Config, "SessionProfiles", "session-c.xml");
        using var stream = Storage.OpenFile(path, OpenFileMode.Read);
        var root = XElement.Load(stream);

        Assert.Equal("ModProfile", root.Name.LocalName);
        Assert.Equal("session-c", root.Attribute(nameof(ModProfile.Id))?.Value);
    }

    public void Dispose()
    {
        _globalProfileBackup.Dispose();
        _sessionProfilesBackup.Dispose();
        _worldsBackup.Dispose();
    }

    private static void SaveGlobalProfile(string xml)
    {
        EnsureConfigDirectory();
        using var stream = Storage.OpenFile(ModProfileManager.GlobalProfilePath, OpenFileMode.Create);
        var root = XElement.Parse(xml);
        root.Save(stream);
    }

    private static void EnsureConfigDirectory()
    {
        if (!Storage.DirectoryExists(GamePaths.Config))
        {
            Storage.CreateDirectory(GamePaths.Config);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (Storage.FileExists(path))
        {
            Storage.DeleteFile(path);
        }
    }

    private static void DeleteDirectoryIfExists(string path)
    {
        if (Storage.DirectoryExists(path))
        {
            DeleteDirectoryRecursive(path);
        }
    }

    private static string CreateWorldDirectory(
        string worldName,
        ModProfileResolutionStrategy strategy,
        string? directoryName = null)
    {
        if (!Storage.DirectoryExists(GamePaths.Worlds))
        {
            Storage.CreateDirectory(GamePaths.Worlds);
        }

        var worldDirectoryName = Storage.CombinePaths(GamePaths.Worlds, directoryName ?? worldName);
        if (!Storage.DirectoryExists(worldDirectoryName))
        {
            Storage.CreateDirectory(worldDirectoryName);
        }

        var worldSettings = new WorldSettings
        {
            Name = worldName,
            Seed = "seed",
            ModProfileResolutionStrategy = strategy
        };
        var rootNode = new ValuesDictionary();
        var subsystems = new ValuesDictionary();
        var gameInfo = new ValuesDictionary();
        rootNode.SetValue("Version", VersionsManager.SerializationVersion);
        rootNode.SetValue("Subsystems", subsystems);
        subsystems.SetValue("GameInfo", gameInfo);
        worldSettings.Save(gameInfo, false);
        gameInfo.SetValue("WorldDirectoryName", worldDirectoryName);
        gameInfo.SetValue("WorldSeed", 1);

        using var stream = Storage.OpenFile(Storage.CombinePaths(worldDirectoryName, "Project.json"), OpenFileMode.Create);
        using var writer = new StreamWriter(stream);
        writer.Write(rootNode.ToJsonText());

        return worldDirectoryName;
    }

    private sealed class FileBackup : IDisposable
    {
        private readonly string _path;
        private readonly string _backupPath;
        private readonly bool _hadOriginal;

        private FileBackup(string path, string backupPath, bool hadOriginal)
        {
            _path = path;
            _backupPath = backupPath;
            _hadOriginal = hadOriginal;
        }

        public static FileBackup Create(string path)
        {
            var backupPath = Storage.CombinePaths(GamePaths.Config, $"{Guid.NewGuid():N}.bak");
            var hadOriginal = Storage.FileExists(path);
            if (hadOriginal)
            {
                EnsureParentDirectory(backupPath);
                Storage.CopyFile(path, backupPath);
            }

            return new FileBackup(path, backupPath, hadOriginal);
        }

        public void Dispose()
        {
            if (Storage.FileExists(_path))
            {
                Storage.DeleteFile(_path);
            }

            if (_hadOriginal && Storage.FileExists(_backupPath))
            {
                EnsureParentDirectory(_path);
                Storage.CopyFile(_backupPath, _path);
                Storage.DeleteFile(_backupPath);
            }
        }
    }

    private sealed class DirectoryBackup : IDisposable
    {
        private readonly string _path;
        private readonly string _backupPath;
        private readonly bool _hadOriginal;

        private DirectoryBackup(string path, string backupPath, bool hadOriginal)
        {
            _path = path;
            _backupPath = backupPath;
            _hadOriginal = hadOriginal;
        }

        public static DirectoryBackup Create(string path)
        {
            var backupPath = Storage.CombinePaths(GamePaths.Config, $"{Guid.NewGuid():N}.dirbak");
            var hadOriginal = Storage.DirectoryExists(path);
            if (hadOriginal)
            {
                CopyDirectory(path, backupPath);
            }

            return new DirectoryBackup(path, backupPath, hadOriginal);
        }

        public void Dispose()
        {
            if (Storage.DirectoryExists(_path))
            {
                DeleteDirectoryRecursive(_path);
            }

            if (_hadOriginal && Storage.DirectoryExists(_backupPath))
            {
                CopyDirectory(_backupPath, _path);
                DeleteDirectoryRecursive(_backupPath);
            }
        }
    }

    private static void EnsureParentDirectory(string path)
    {
        var directory = Storage.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Storage.DirectoryExists(directory))
        {
            Storage.CreateDirectory(directory);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        if (!Storage.DirectoryExists(destination))
        {
            Storage.CreateDirectory(destination);
        }

        foreach (var file in Storage.ListFileNames(source))
        {
            var sourcePath = Storage.CombinePaths(source, file);
            var destinationPath = Storage.CombinePaths(destination, file);
            Storage.CopyFile(sourcePath, destinationPath);
        }

        foreach (var directory in Storage.ListDirectoryNames(source))
        {
            CopyDirectory(
                Storage.CombinePaths(source, directory),
                Storage.CombinePaths(destination, directory));
        }
    }

    private static void DeleteDirectoryRecursive(string path)
    {
        foreach (var file in Storage.ListFileNames(path))
        {
            Storage.DeleteFile(Storage.CombinePaths(path, file));
        }

        foreach (var directory in Storage.ListDirectoryNames(path))
        {
            DeleteDirectoryRecursive(Storage.CombinePaths(path, directory));
        }

        Storage.DeleteDirectory(path);
    }
}
