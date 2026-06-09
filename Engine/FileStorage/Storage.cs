using System.Text;

#if ANDROID
using Android.App;
using Engine.Windowing;
using AndroidEnvironment = Android.OS.Environment;
using Environment = System.Environment;
using Android.OS;
#endif

#if DESKTOP
using System.Diagnostics;
#endif

namespace Engine.FileStorage;

public static class Storage
{
#if DESKTOP
    private static bool _dataDirectoryCreated;

    private static readonly Lock _dataDirectoryCreationLock = new();
#endif

    public static long FreeSpace
    {
        get
        {
#if ANDROID
            try
            {
                var statFs = new StatFs(AndroidEnvironment.DataDirectory?.Path);
                var num = statFs.BlockSizeLong;
                return statFs.AvailableBlocksLong * num;
            }
            catch (Exception)
            {
                return long.MaxValue;
            }
#endif
#if DESKTOP
            var fullPath = Path.GetFullPath(ProcessPath("data:", false, false));
            if (fullPath.Length <= 0)
            {
                return long.MaxValue;
            }

            try
            {
                return new DriveInfo(fullPath[..1]).AvailableFreeSpace;
            }
            catch
            {
                // ignored
            }

            return long.MaxValue;
#endif
        }
    }

    public static bool FileExists(string path)
    {
#if ANDROID
        var path2 = ProcessPath(path, true, false, out var isApp);
        if (isApp)
        {
            return Application.Context.Assets?
                .List(GetDirectoryName(path2))?
                .Contains(GetFileName(path2)) ?? false;
        }

        return File.Exists(ProcessPath(path, false, true));
#endif
#if DESKTOP
        return File.Exists(ProcessPath(path, false, false));
#endif
    }

    public static bool DirectoryExists(string path) => Directory.Exists(ProcessPath(path, false, false));

    public static long GetFileSize(string path) => new FileInfo(ProcessPath(path, false, false)).Length;

    public static DateTime GetFileLastWriteTime(string path) =>
        File.GetLastWriteTimeUtc(ProcessPath(path, false, false));

    public static Stream OpenFile(string path, OpenFileMode openFileMode)
    {
        if (openFileMode != 0 &&
            openFileMode != OpenFileMode.ReadWrite &&
            openFileMode != OpenFileMode.Create &&
            openFileMode != OpenFileMode.CreateOrOpen)
        {
            throw new ArgumentException(null, nameof(openFileMode));
        }
#if ANDROID
        var path2 = ProcessPath(path, true, false, out var isApp);
        if (isApp)
        {
            var stream = Application.Context.Assets?.Open(path2);
            return stream ?? throw new FileNotFoundException($"File: {path} not found");
        }
#endif
#if DESKTOP
        var path2 = ProcessPath(path, openFileMode != OpenFileMode.Read, false);
#endif
        var mode = openFileMode switch
        {
            OpenFileMode.Create => FileMode.Create,
            OpenFileMode.CreateOrOpen => FileMode.OpenOrCreate,
            _ => FileMode.Open
        };

        var access = openFileMode == OpenFileMode.Read ? FileAccess.Read : FileAccess.ReadWrite;
        return File.Open(path2, mode, access, FileShare.ReadWrite);
    }

    public static void DeleteFile(string path) => File.Delete(ProcessPath(path, true, false));

    public static void CopyFile(string sourcePath, string destinationPath)
    {
        using var stream = OpenFile(sourcePath, OpenFileMode.Read);
        using var destination = OpenFile(destinationPath, OpenFileMode.Create);
        stream.CopyTo(destination);
    }

    public static void MoveFile(string sourcePath, string destinationPath)
    {
        var sourceFileName = ProcessPath(sourcePath, true, false);
        var text = ProcessPath(destinationPath, true, false);
        File.Delete(text);
        File.Move(sourceFileName, text);
    }

    public static void CreateDirectory(string path) => Directory.CreateDirectory(ProcessPath(path, true, false));

    public static void DeleteDirectory(string path) => Directory.Delete(ProcessPath(path, true, false));

    public static IEnumerable<string> ListFileNames(string path)
    {
        return from s in Directory.EnumerateFiles(ProcessPath(path, false, false))
#if ANDROID
            select Path.GetFileName(s)
            into s
            where s != ".__override__"
            select s;
#endif
#if DESKTOP
            select Path.GetFileName(s);
#endif
    }

    public static IEnumerable<string> ListDirectoryNames(string path)
    {
        return from s in Directory.EnumerateDirectories(ProcessPath(path, false, false))
            select Path.GetFileName(s);
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

    public static string GetSystemPath(string path) => ProcessPath(path, false, false);

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
            if (paths[i].Length <= 0)
            {
                continue;
            }

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

#if ANDROID
    public static string ProcessPath(string path, bool writeAccess, bool failIfApp) =>
        ProcessPath(path, writeAccess, failIfApp, out _);

    private static string ProcessPath(string path, bool writeAccess, bool failIfApp, out bool isApp)
    {
        path =  NormalizePath(path);
        if (path.StartsWith("app:"))
        {
            if (failIfApp)
            {
                throw new InvalidOperationException($"Access denied to \"{path}\".");
            }

            isApp = true;
            return path[4..].TrimStart(Path.DirectorySeparatorChar);
        }

        if (path.StartsWith("data:"))
        {
            isApp = false;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                path[5..].TrimStart(Path.DirectorySeparatorChar)
            );
        }

        if (path.StartsWith("android:"))
        {
            isApp = false;
            return GetExternalStoragePath(path, "android:");
        }

        if (path.StartsWith("config:"))
        {
            isApp = false;
            return CombinePaths(
                GetExternalStoragePath(RunPath.ConfigPath, "android:"),
                path["config:".Length..].TrimStart(Path.DirectorySeparatorChar)
            );
        }

        throw new InvalidOperationException($"Invalid path \"{path}\".");

        // 局部方法，获取输入路径的绝对路径，支持去除前缀
        string GetExternalStoragePath(string inputPath, string trimPrefix = "")
        {
            var absolutePath = AndroidEnvironment.ExternalStorageDirectory?.AbsolutePath ?? string.Empty;
            var noPrefixPath = inputPath[trimPrefix.Length..].TrimStart(Path.DirectorySeparatorChar);
            return CombinePaths(absolutePath, noPrefixPath);
        }
    }
#endif
#if DESKTOP
    private static string ProcessPath(string path, bool writeAccess, bool failIfApp)
    {
        path = NormalizePath(path);

        var baseDirectory = string.Empty;
        if (path.StartsWith("app:"))
        {
            baseDirectory = GetAppDirectory(failIfApp);
            path = path[4..].TrimStart(Path.DirectorySeparatorChar);
        }
        else if (path.StartsWith("data:"))
        {
            baseDirectory = GetDataDirectory(writeAccess);
            path = path[5..].TrimStart(Path.DirectorySeparatorChar);
        }
        else if (path.StartsWith("config:"))
        {
            baseDirectory = GetAppDirectory(failIfApp);
            var configPath = RunPath.ConfigPath[4..].TrimStart(Path.DirectorySeparatorChar);
            path = CombinePaths(configPath, path[7..].TrimStart(Path.DirectorySeparatorChar))
                .TrimStart(Path.DirectorySeparatorChar);
        }
        else
        {
            if (!path.StartsWith("system:"))
            {
                throw new InvalidOperationException("Invalid path.");
            }

            path = path[7..];
        }

        return !string.IsNullOrEmpty(baseDirectory) ? Path.Combine(baseDirectory, path) : path;
    }

    private static string GetAppDirectory(bool failIfApp)
    {
        return failIfApp
            ? throw new InvalidOperationException("Access denied.")
            : Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location) ?? string.Empty;
    }

    private static string GetDataDirectory(bool writeAccess)
    {
        var text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty);
        if (!writeAccess)
        {
            return text;
        }

        lock (_dataDirectoryCreationLock)
        {
            if (_dataDirectoryCreated)
            {
                return text;
            }

            Directory.CreateDirectory(text);
            _dataDirectoryCreated = true;
            return text;
        }
    }
#endif

    public static void OpenFileWithExternalApplication(string path, string? chooserTitle = null,
        string? mimeType = null)
    {
        path = NormalizePath(path);
        if (!FileExists(path))
        {
            throw new FileNotFoundException($"Open {path} failed, because it is not exists.");
        }

        path = ProcessPath(path, false, false);
#if DESKTOP
        if (OperatingSystem.IsWindows())
        {
            Process.Start("explorer.exe", path);
        }

        if (OperatingSystem.IsLinux())
        {
            Process.Start("xdg-open", path);
        }
#endif
#if ANDROID
        Window.ActivityInstance.OpenFile(path, chooserTitle, mimeType);
#endif
    }

    public static void MoveDirectory(string path, string newPath)
    {
        Directory.Move(ProcessPath(path, true, false), ProcessPath(newPath, true, false));
    }

    public static void DeleteDirectoryRecursive(string path)
    {
        Directory.Delete(ProcessPath(path, true, false), true);
    }

    public static DirectoryInfo GetDirectoryInfo(string path) => new(ProcessPath(path, true, false));

    public static FileInfo GetFileInfo(string path) => new(ProcessPath(path, true, false));

    /// <summary>
    /// 规范化路径：将路径中的所有分隔符（/ 和 \）统一替换为当前系统的 Path.DirectorySeparatorChar
    /// </summary>
    /// <param name="path">原始路径字符串</param>
    /// <returns>规范化后的路径</returns>
    public static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        path = path.Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        path = path.Trim();

        return path;
    }
}
