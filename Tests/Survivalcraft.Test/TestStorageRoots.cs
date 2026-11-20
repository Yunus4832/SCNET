using System.Runtime.CompilerServices;

using Engine.Core.FileStorage;

using Game.Application;
using Game.FileStorage;
using Game.Managers;
using Game.Modding;
using Game.World;

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
        ClientApplication.ConfigurePaths(
            new ClientStoragePaths(
                GamePaths.External,
                GamePaths.ScreenCaptures,
                GamePaths.CharacterSkins,
                GamePaths.FurniturePacks,
                GamePaths.BlockTextures,
                GamePaths.ContentPackageCache,
                GamePaths.ContentPackageCreationTemp,
                GamePaths.Logs
            )
        );
        ClientApplication.InstallWorldRepository(
            new WorldRepository(
                GamePaths.Worlds,
                [],
                GameManager.WorldActivity,
                WorldProjectPersistence.WaitForPendingSaves
            )
        );
        ModProfileManager.Initialize(ClientApplication.Worlds);
    }
}
