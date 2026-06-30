using System.IO.Compression;
using System.Text;

using Engine.FileStorage;

using Game;
using Game.Modding;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public sealed class LocalModsImportManagerTest : IDisposable
{
    private readonly string _sourceDirectory = Path.Combine(Path.GetTempPath(), $"scnet-installed-mods-{Guid.NewGuid():N}");
    private readonly string _repositoryDirectory = Path.Combine(Path.GetTempPath(), $"scnet-mod-cache-{Guid.NewGuid():N}");
    private readonly FileBackup _stateBackup = FileBackup.Create(LocalModsImportManager.ImportStatePath);

    [Fact]
    public void ImportInstalledModsCopiesPackageOnlyOnceWhenUnchanged()
    {
        Directory.CreateDirectory(_sourceDirectory);
        Directory.CreateDirectory(_repositoryDirectory);
        var sourcePath = Path.Combine(_sourceDirectory, "example.alpha.scpak");
        File.WriteAllBytes(sourcePath, CreatePackageBytes("example.alpha", "1.0.0"));

        LocalModsImportManager.ImportInstalledMods(_sourceDirectory, _repositoryDirectory);

        var repository = new LocalModRepository(_repositoryDirectory);
        var imported = Assert.Single(repository.ListAll());
        var initialWriteTimeUtc = File.GetLastWriteTimeUtc(imported.Path);

        Thread.Sleep(1100);
        LocalModsImportManager.ImportInstalledMods(_sourceDirectory, _repositoryDirectory);

        repository.Invalidate();
        var reloaded = Assert.Single(repository.ListAll());
        Assert.Equal(imported.PackageHash, reloaded.PackageHash);
        Assert.Equal(initialWriteTimeUtc, File.GetLastWriteTimeUtc(reloaded.Path));
    }

    [Fact]
    public void ImportInstalledModsReimportsPackageWhenRepositoryEntryIsMissing()
    {
        Directory.CreateDirectory(_sourceDirectory);
        Directory.CreateDirectory(_repositoryDirectory);
        var sourcePath = Path.Combine(_sourceDirectory, "example.alpha.scpak");
        File.WriteAllBytes(sourcePath, CreatePackageBytes("example.alpha", "1.0.0"));

        LocalModsImportManager.ImportInstalledMods(_sourceDirectory, _repositoryDirectory);

        var repository = new LocalModRepository(_repositoryDirectory);
        var imported = Assert.Single(repository.ListAll());
        File.Delete(imported.Path);

        LocalModsImportManager.ImportInstalledMods(_sourceDirectory, _repositoryDirectory);

        repository.Invalidate();
        var restored = Assert.Single(repository.ListAll());
        Assert.True(File.Exists(restored.Path));
    }

    [Fact]
    public void ExportPackageCopiesPackageAndRecordsImportState()
    {
        Directory.CreateDirectory(_sourceDirectory);
        Directory.CreateDirectory(_repositoryDirectory);
        var repository = new LocalModRepository(_repositoryDirectory);
        var sourcePath = Path.Combine(_sourceDirectory, "example.alpha.scpak");
        File.WriteAllBytes(sourcePath, CreatePackageBytes("example.alpha", "1.0.0"));
        var entry = repository.ImportPackage(sourcePath);
        var exportDirectory = Path.Combine(Path.GetTempPath(), $"scnet-exported-mods-{Guid.NewGuid():N}");
        try
        {
            var exportedPath = LocalModsImportManager.ExportPackage(entry, exportDirectory);

            Assert.True(File.Exists(exportedPath));
            var imported = Assert.Single(LocalModsImportManager.ListImportedMods());
            Assert.Equal(exportedPath, imported.Path);
            Assert.Equal(entry.PackageHash, imported.PackageHash);
        }
        finally
        {
            if (Directory.Exists(exportDirectory))
            {
                Directory.Delete(exportDirectory, true);
            }
        }
    }

    public void Dispose()
    {
        _stateBackup.Dispose();
        if (Directory.Exists(_sourceDirectory))
        {
            Directory.Delete(_sourceDirectory, true);
        }

        if (Directory.Exists(_repositoryDirectory))
        {
            Directory.Delete(_repositoryDirectory, true);
        }
    }

    private static byte[] CreatePackageBytes(string modId, string version)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8, leaveOpen: false);
            writer.Write($$"""
                           {
                             "id": "{{modId}}",
                             "name": "{{modId}}",
                             "version": "{{version}}"
                           }
                           """);
        }

        return stream.ToArray();
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

        private static void EnsureParentDirectory(string path)
        {
            var directory = Storage.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Storage.DirectoryExists(directory))
            {
                Storage.CreateDirectory(directory);
            }
        }
    }
}
