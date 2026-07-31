using Android.Content.Res;

namespace Survivalcraft.Android;

internal sealed class AndroidAssetsStorageRoot(AssetManager assets) : IStorageRoot
{
    public long FreeSpace => long.MaxValue;

    public bool FileExists(string path)
    {
        var directoryName = Storage.GetDirectoryName(path);
        return assets.List(ToAssetPath(directoryName))?.Contains(Storage.GetFileName(path)) ?? false;
    }

    public bool DirectoryExists(string path)
    {
        return assets.List(ToAssetPath(path)) is { Length: > 0 };
    }

    public long GetFileSize(string path)
    {
        using var stream = OpenFile(path, OpenFileMode.Read);
        return stream.Length;
    }

    public DateTime GetFileLastWriteTime(string path) => DateTime.MinValue;

    public Stream OpenFile(string path, OpenFileMode openFileMode)
    {
        if (openFileMode != OpenFileMode.Read)
        {
            throw new InvalidOperationException("Android application assets are read-only.");
        }

        return assets.Open(ToAssetPath(path))
               ?? throw new FileNotFoundException($"Asset \"{path}\" was not found.");
    }

    public void DeleteFile(string path) => ThrowReadOnly();

    public void MoveFile(string sourcePath, string destinationPath) => ThrowReadOnly();

    public void CreateDirectory(string path) => ThrowReadOnly();

    public void DeleteDirectory(string path, bool recursive) => ThrowReadOnly();

    public void MoveDirectory(string sourcePath, string destinationPath) => ThrowReadOnly();

    public IEnumerable<string> ListFileNames(string path)
    {
        return ListEntries(path).Where(name => FileExists(Storage.CombinePaths(path, name)));
    }

    public IEnumerable<string> ListDirectoryNames(string path)
    {
        return ListEntries(path).Where(name => DirectoryExists(Storage.CombinePaths(path, name)));
    }

    public string GetSystemPath(string path)
    {
        throw new InvalidOperationException("Android application assets do not have system file paths.");
    }

    private IEnumerable<string> ListEntries(string path)
    {
        return assets.List(ToAssetPath(path)) ?? [];
    }

    private static string ToAssetPath(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    private static void ThrowReadOnly() =>
        throw new InvalidOperationException("Android application assets are read-only.");
}
