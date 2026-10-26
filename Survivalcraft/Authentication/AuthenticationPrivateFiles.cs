using System.Xml.Linq;

namespace Game.Authentication;

internal static class AuthenticationPrivateFiles
{
    internal static XElement? Read(string path)
    {
        if (!File.Exists(path)) { return null; }

        if (new FileInfo(path).Length > 4 * 1024 * 1024)
        {
            throw new InvalidDataException("Authentication configuration exceeds the size limit.");
        }

        return XElement.Load(path);
    }

    internal static void Save(string path, XElement data)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsAndroid())
            {
                File.SetUnixFileMode(
                    directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                );
            }
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporary, options))
            {
                data.Save(stream);
                if (stream.Position > 4 * 1024 * 1024)
                {
                    throw new InvalidOperationException("Authentication configuration exceeds the size limit.");
                }

                stream.Flush(true);
            }

            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary)) { File.Delete(temporary); }
        }
    }
}
