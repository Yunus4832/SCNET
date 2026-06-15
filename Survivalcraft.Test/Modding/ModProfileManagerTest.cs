using System.Xml.Linq;

using Engine.FileStorage;

using Game;
using Game.Modding;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class ModProfileManagerTest : IDisposable
{
    private readonly FileBackup _globalProfileBackup = FileBackup.Create(ModProfileManager.GlobalProfilePath);
    private readonly DirectoryBackup _sessionProfilesBackup =
        DirectoryBackup.Create(Storage.CombinePaths(GamePaths.Config, "SessionProfiles"));

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
        ModProfileManager.SaveSessionProfile("session-a", new ModProfile
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
    public void LoadEffectiveProfileFallsBackToDefaultWhenNoProfileExists()
    {
        DeleteIfExists(ModProfileManager.GlobalProfilePath);
        DeleteDirectoryIfExists(Storage.CombinePaths(GamePaths.Config, "SessionProfiles"));

        var profile = ModProfileManager.LoadEffectiveProfile("missing");

        Assert.Equal("default", profile.Id);
        Assert.Null(profile.RepositoryUrl);
        Assert.Empty(profile.Packages);
    }

    public void Dispose()
    {
        _globalProfileBackup.Dispose();
        _sessionProfilesBackup.Dispose();
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
