using System.Text;

namespace Engine.FileStorage;

public static class Storage
{
    private static readonly Dictionary<string, IStorageRoot> _roots = new(StringComparer.Ordinal);

    private static readonly Lock _rootsLock = new();

    public static long FreeSpace => GetRoot("data").FreeSpace;

    public static void RegisterFileSystemRoot(string name, string rootPath, bool readOnly = false,
        bool allowEscapingRoot = false)
    {
        RegisterRoot(name, new FileSystemStorageRoot(rootPath, readOnly, allowEscapingRoot));
    }

    public static void RegisterRoot(string name, IStorageRoot root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(root);
        if (name.Contains(':') || name.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException("Storage root name cannot contain a colon or path separator.", nameof(name));
        }

        lock (_rootsLock)
        {
            _roots[name] = root;
        }
    }

    public static bool FileExists(string path)
    {
        var resolvedPath = ResolvePath(path);
        return resolvedPath.Root.FileExists(resolvedPath.RelativePath);
    }

    public static bool DirectoryExists(string path)
    {
        var resolvedPath = ResolvePath(path);
        return resolvedPath.Root.DirectoryExists(resolvedPath.RelativePath);
    }

    public static long GetFileSize(string path)
    {
        var resolvedPath = ResolvePath(path);
        return resolvedPath.Root.GetFileSize(resolvedPath.RelativePath);
    }

    public static DateTime GetFileLastWriteTime(string path)
    {
        var resolvedPath = ResolvePath(path);
        return resolvedPath.Root.GetFileLastWriteTime(resolvedPath.RelativePath);
    }

    public static Stream OpenFile(string path, OpenFileMode openFileMode)
    {
        if (openFileMode != OpenFileMode.Read &&
            openFileMode != OpenFileMode.ReadWrite &&
            openFileMode != OpenFileMode.Create &&
            openFileMode != OpenFileMode.CreateOrOpen)
        {
            throw new ArgumentException(null, nameof(openFileMode));
        }

        var resolvedPath = ResolvePath(path);
        return resolvedPath.Root.OpenFile(resolvedPath.RelativePath, openFileMode);
    }

    public static void DeleteFile(string path)
    {
        var resolvedPath = ResolvePath(path);
        resolvedPath.Root.DeleteFile(resolvedPath.RelativePath);
    }

    public static void CopyFile(string sourcePath, string destinationPath)
    {
        using var stream = OpenFile(sourcePath, OpenFileMode.Read);
        using var destination = OpenFile(destinationPath, OpenFileMode.Create);
        stream.CopyTo(destination);
    }

    public static void MoveFile(string sourcePath, string destinationPath)
    {
        var source = ResolvePath(sourcePath);
        var destination = ResolvePath(destinationPath);
        if (ReferenceEquals(source.Root, destination.Root))
        {
            source.Root.MoveFile(source.RelativePath, destination.RelativePath);
            return;
        }

        CopyFile(sourcePath, destinationPath);
        source.Root.DeleteFile(source.RelativePath);
    }

    public static void CreateDirectory(string path)
    {
        var resolvedPath = ResolvePath(path);
        resolvedPath.Root.CreateDirectory(resolvedPath.RelativePath);
    }

    public static void DeleteDirectory(string path)
    {
        var resolvedPath = ResolvePath(path);
        resolvedPath.Root.DeleteDirectory(resolvedPath.RelativePath, false);
    }

    public static IEnumerable<string> ListFileNames(string path)
    {
        var resolvedPath = ResolvePath(path);
        return resolvedPath.Root.ListFileNames(resolvedPath.RelativePath);
    }

    public static IEnumerable<string> ListDirectoryNames(string path)
    {
        var resolvedPath = ResolvePath(path);
        return resolvedPath.Root.ListDirectoryNames(resolvedPath.RelativePath);
    }

    public static string ReadAllText(string path) => ReadAllText(path, Encoding.UTF8);

    public static string ReadAllText(string path, Encoding encoding)
    {
        using var streamReader = new StreamReader(OpenFile(path, OpenFileMode.Read), encoding);
        return streamReader.ReadToEnd();
    }

    public static void WriteAllText(string path, string text) => WriteAllText(path, text, Encoding.UTF8);

    public static void WriteAllText(string path, string text, Encoding encoding)
    {
        using var streamWriter = new StreamWriter(OpenFile(path, OpenFileMode.Create), encoding);
        streamWriter.Write(text);
    }

    public static byte[] ReadAllBytes(string path)
    {
        using var binaryReader = new BinaryReader(OpenFile(path, OpenFileMode.Read));
        return binaryReader.ReadBytes((int)binaryReader.BaseStream.Length);
    }

    public static void WriteAllBytes(string path, byte[] bytes)
    {
        using var binaryWriter = new BinaryWriter(OpenFile(path, OpenFileMode.Create));
        binaryWriter.Write(bytes);
    }

    public static string GetSystemPath(string path)
    {
        var resolvedPath = ResolvePath(path);
        return resolvedPath.Root.GetSystemPath(resolvedPath.RelativePath);
    }

    public static string GetExtension(string path)
    {
        path = NormalizePath(path);
        var num = path.LastIndexOf('.');
        return num >= 0 ? path[num..] : string.Empty;
    }

    public static string GetFileName(string path)
    {
        path = NormalizePath(path);
        var num = path.LastIndexOf(Path.DirectorySeparatorChar);
        return num >= 0 ? path[(num + 1)..] : path;
    }

    public static string GetFileNameWithoutExtension(string path)
    {
        var fileName = GetFileName(path);
        var num = fileName.LastIndexOf('.');
        return num >= 0 ? fileName[..num] : fileName;
    }

    public static string GetDirectoryName(string path)
    {
        path = NormalizePath(path);
        var num = path.LastIndexOf(Path.DirectorySeparatorChar);
        return num >= 0 ? path[..num].TrimEnd(Path.DirectorySeparatorChar) : string.Empty;
    }

    public static string CombinePaths(params string[] paths)
    {
        var stringBuilder = new StringBuilder();
        for (var i = 0; i < paths.Length; i++)
        {
            if (string.IsNullOrEmpty(paths[i]))
            {
                continue;
            }

            paths[i] = NormalizePath(paths[i]);
            stringBuilder.Append(paths[i]);
            if (i >= paths.Length - 1 ||
                (stringBuilder.Length != 0 && stringBuilder[^1] == Path.DirectorySeparatorChar))
            {
                continue;
            }

            stringBuilder.Append(Path.DirectorySeparatorChar);
        }

        return stringBuilder.ToString();
    }

    public static string ChangeExtension(string path, string extension)
    {
        return CombinePaths(GetDirectoryName(path), GetFileNameWithoutExtension(path)) + extension;
    }

    public static void MoveDirectory(string path, string newPath)
    {
        var source = ResolvePath(path);
        var destination = ResolvePath(newPath);
        if (!ReferenceEquals(source.Root, destination.Root))
        {
            throw new InvalidOperationException("Moving directories between storage roots is not supported.");
        }

        source.Root.MoveDirectory(source.RelativePath, destination.RelativePath);
    }

    public static void DeleteDirectoryRecursive(string path)
    {
        var resolvedPath = ResolvePath(path);
        resolvedPath.Root.DeleteDirectory(resolvedPath.RelativePath, true);
    }

    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        return path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .Trim();
    }

    private static ResolvedStoragePath ResolvePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = NormalizePath(path);
        var separatorIndex = path.IndexOf(':');
        if (separatorIndex <= 0)
        {
            throw new InvalidOperationException($"Invalid storage path \"{path}\".");
        }

        var rootName = path[..separatorIndex];
        var relativePath = path[(separatorIndex + 1)..].TrimStart(Path.DirectorySeparatorChar);
        return new ResolvedStoragePath(GetRoot(rootName), relativePath);
    }

    private static IStorageRoot GetRoot(string name)
    {
        lock (_rootsLock)
        {
            if (_roots.TryGetValue(name, out var root))
            {
                return root;
            }
        }

        throw new InvalidOperationException($"Storage root \"{name}:\" is not registered.");
    }

    private readonly record struct ResolvedStoragePath(IStorageRoot Root, string RelativePath);
}
