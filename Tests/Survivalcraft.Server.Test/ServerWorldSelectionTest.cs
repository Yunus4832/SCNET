using Engine.Core.FileStorage;

using Game.FileStorage;
using Game.Hosting;
using Game.Managers;
using Game.Modding;
using Game.Startup;
using Game.World;

namespace Survivalcraft.Server.Test;

/// <summary>验证服务器创建时保存已准备组合，重新选择已有世界时保留其完整配置。</summary>
[Collection(ServerStarterCollection.Name)]
public sealed class ServerWorldSelectionTest
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NewWorldPersistsTheExactPreparedProfile(bool empty)
    {
        using var runtime = GameModRuntime.StartFromProfile(
            new ModProfile(),
            Storage.GetSystemPath(GamePaths.ContentPackageCache),
            includeClientAssets: false
        );
        runtime.InitializeDatabase();
        var worlds = new WorldRepository(GamePaths.Worlds, [], new WorldProjectActivity(), static () => { });
        var worldName = "Prepared-" + Guid.NewGuid().ToString("N");
        var directory = Storage.CombinePaths(GamePaths.Worlds, worldName);
        var prepared = empty ? new ModProfile() : CreateProfile();
        var session = new SessionInfo { Target = SessionTarget.World, World = worldName, Seed = "prepared-seed" };
        try
        {
            var world = ServerWorldSelection.Resolve(
                new StartupContext(new RunningSetting(), new StartupRequest(), session),
                worlds,
                DatabaseManager.GameDatabase,
                prepared
            );

            Assert.Equal(directory, world.DirectoryName);
            Assert.True(world.Hosting.RunServer);
            var saved = ModProfileManager.LoadWorldProfile(directory);
            Assert.NotNull(saved);
            Assert.True(ModProfileIdentity.AreEquivalent(prepared, saved));
            Assert.Equal("prepared-seed", world.WorldSettings.Seed);
        }
        finally
        {
            if (Storage.DirectoryExists(directory)) { Storage.DeleteDirectoryRecursive(directory); }
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExistingWorldRetainsItsProfileWhenSelectedWithAnotherCreationTemplate(bool runServer, bool empty)
    {
        using var runtime = GameModRuntime.StartFromProfile(
            new ModProfile(),
            Storage.GetSystemPath(GamePaths.ContentPackageCache),
            includeClientAssets: false
        );
        runtime.InitializeDatabase();
        var worlds = new WorldRepository(GamePaths.Worlds, [], new WorldProjectActivity(), static () => { });
        var worldName = "Existing-" + Guid.NewGuid().ToString("N");
        var directory = Storage.CombinePaths(GamePaths.Worlds, Guid.NewGuid().ToString("N"));
        try
        {
            worlds.CreateWorld(
                DatabaseManager.GameDatabase,
                new WorldSettings { Name = worldName, Seed = "saved-seed" },
                new WorldHostingSettings { RunServer = runServer },
                empty ? new ModProfile() : CreateProfile(),
                directory
            );
            var profilePath = Storage.GetSystemPath(ModProfileManager.GetWorldProfilePath(directory));
            var original = File.ReadAllBytes(profilePath);
            var session = new SessionInfo { Target = SessionTarget.World, World = worldName, Seed = "ignored-seed" };

            var world = ServerWorldSelection.Resolve(
                new StartupContext(new RunningSetting(), new StartupRequest(), session),
                worlds,
                DatabaseManager.GameDatabase,
                empty ? CreateProfile() : new ModProfile()
            );

            Assert.Equal(directory, world.DirectoryName);
            Assert.Equal(original, File.ReadAllBytes(profilePath));
            Assert.Equal("saved-seed", world.WorldSettings.Seed);
            Assert.True(world.Hosting.RunServer);
            Assert.False(Storage.DirectoryExists(Storage.CombinePaths(GamePaths.Worlds, worldName)));
        }
        finally
        {
            if (Storage.DirectoryExists(directory)) { Storage.DeleteDirectoryRecursive(directory); }
        }
    }

    private static ModProfile CreateProfile()
    {
        return new ModProfile
        {
            Packages =
            [
                new ModPackageRequirement
                {
                    ModId = "prepared.mod",
                    Version = "2.0.0",
                    PackageHash = new string('b', 64)
                }
            ]
        };
    }
}
