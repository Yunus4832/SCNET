using System.Runtime.CompilerServices;

using Engine.FileStorage;

namespace Survivalcraft.Test;

internal static class TestStorageRoots
{
    [ModuleInitializer]
    internal static void Register()
    {
        var appPath = AppContext.BaseDirectory;
        Storage.RegisterFileSystemRoot("app", appPath, readOnly: true);
        Storage.RegisterFileSystemRoot("starter", appPath);
        Storage.RegisterFileSystemRoot("external", appPath);
        Storage.RegisterFileSystemRoot("data", Path.Combine(appPath, "Data"));
        Storage.RegisterFileSystemRoot("config", Path.Combine(appPath, "Config"));
        Storage.RegisterFileSystemRoot("system", Path.GetPathRoot(appPath) ?? appPath, allowEscapingRoot: true);
    }
}
