namespace Engine.FileStorage;

public sealed class FileSystemStorageRoot : IStorageRoot
{
    private readonly string _rootPath;
    private readonly bool _readOnly;
    private readonly bool _allowEscapingRoot;

    public FileSystemStorageRoot(string rootPath, bool readOnly = false, bool allowEscapingRoot = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var fullPath = Path.GetFullPath(rootPath);
        _rootPath = fullPath == Path.GetPathRoot(fullPath)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        _readOnly = readOnly;
        _allowEscapingRoot = allowEscapingRoot;
    }

    public long FreeSpace
    {
        get
        {
            try
            {
                return new DriveInfo(Path.GetPathRoot(_rootPath) ?? _rootPath).AvailableFreeSpace;
            }
            catch
            {
                return long.MaxValue;
            }
        }
    }

    public bool FileExists(string path) => File.Exists(ResolvePath(path));

    public bool DirectoryExists(string path) => Directory.Exists(ResolvePath(path));

    public long GetFileSize(string path) => new FileInfo(ResolvePath(path)).Length;

    public DateTime GetFileLastWriteTime(string path) => File.GetLastWriteTimeUtc(ResolvePath(path));

    public Stream OpenFile(string path, OpenFileMode openFileMode)
    {
        EnsureWritable(openFileMode != OpenFileMode.Read);
        var systemPath = ResolvePath(path);
        if (openFileMode != OpenFileMode.Read)
        {
            var directoryPath = Path.GetDirectoryName(systemPath);
            if (!string.IsNullOrEmpty(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        var mode = openFileMode switch
        {
            OpenFileMode.Create => FileMode.Create,
            OpenFileMode.CreateOrOpen => FileMode.OpenOrCreate,
            _ => FileMode.Open
        };
        var access = openFileMode == OpenFileMode.Read ? FileAccess.Read : FileAccess.ReadWrite;
        return File.Open(systemPath, mode, access, FileShare.ReadWrite);
    }

    public void DeleteFile(string path)
    {
        EnsureWritable(true);
        File.Delete(ResolvePath(path));
    }

    public void MoveFile(string sourcePath, string destinationPath)
    {
        EnsureWritable(true);
        var destinationSystemPath = ResolvePath(destinationPath);
        var directoryPath = Path.GetDirectoryName(destinationSystemPath);
        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        File.Move(ResolvePath(sourcePath), destinationSystemPath, true);
    }

    public void CreateDirectory(string path)
    {
        EnsureWritable(true);
        Directory.CreateDirectory(ResolvePath(path));
    }

    public void DeleteDirectory(string path, bool recursive)
    {
        EnsureWritable(true);
        Directory.Delete(ResolvePath(path), recursive);
    }

    public void MoveDirectory(string sourcePath, string destinationPath)
    {
        EnsureWritable(true);
        Directory.Move(ResolvePath(sourcePath), ResolvePath(destinationPath));
    }

    public IEnumerable<string> ListFileNames(string path)
    {
        return Directory.EnumerateFiles(ResolvePath(path)).Select(path => Path.GetFileName(path)!);
    }

    public IEnumerable<string> ListDirectoryNames(string path)
    {
        return Directory.EnumerateDirectories(ResolvePath(path)).Select(path => Path.GetFileName(path)!);
    }

    public string GetSystemPath(string path) => ResolvePath(path);

    private string ResolvePath(string path)
    {
        var relativePath = Storage.NormalizePath(path).TrimStart(Path.DirectorySeparatorChar);
        var resolvedPath = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        if (!_allowEscapingRoot &&
            !resolvedPath.Equals(_rootPath, StringComparison.Ordinal) &&
            !resolvedPath.StartsWith(rootPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path \"{path}\" escapes its storage root.");
        }

        return resolvedPath;
    }

    private void EnsureWritable(bool writeAccess)
    {
        if (_readOnly && writeAccess)
        {
            throw new InvalidOperationException("Storage root is read-only.");
        }
    }
}
