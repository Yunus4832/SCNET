using System.Runtime.CompilerServices;

using Engine.FileStorage;

namespace WorldUpgradeTool.Test;

internal static class TestStorageRoots
{
    [ModuleInitializer]
    internal static void Register()
    {
        var appPath = AppContext.BaseDirectory;
        Storage.RegisterFileSystemRoot("app", appPath);
        Storage.RegisterFileSystemRoot("data", Path.Combine(appPath, "Data"));
        Storage.RegisterFileSystemRoot("system", Path.GetPathRoot(appPath) ?? appPath, allowEscapingRoot: true);
    }
}
