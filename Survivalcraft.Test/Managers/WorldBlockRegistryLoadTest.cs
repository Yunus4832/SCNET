using Engine.Core.FileStorage;
using Engine.Core.Runtime;

using Game.Blocks;
using Game.Configuration;
using Game.Content;
using Game.Hosting;
using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Network;
using Game.Startup;
using Game.World;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Managers;

[Collection(ConfigFileCollection.Name)]
public sealed class WorldBlockRegistryLoadTest
{
    [Fact]
    public void RegistryWriteFailurePreventsWorldConstructionAndReleasesCatalog()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scnet-block-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var original = $"<Project Version=\"{WorldVersions.ProjectFormatVersion}\"><BlockRegistry /></Project>";
        File.WriteAllText(Path.Combine(directory, "Project.xml"), original);
        const string rootName = "block-registry-write-test";
        Storage.RegisterFileSystemRoot(rootName, directory, readOnly: true);

        try
        {
            Dispatcher.Initialize();
            ContentManager.Initialize();
            using var runtime = GameModRuntime.Start(
                [
                    new ModDescriptor(new ModManifest("test", "Metadata", "1.0.0"), static () => new MetadataMod())
                ]
            );
            runtime.InitializeContentData();
            var node = new NetNode(
                NetNodeOptions.Default,
                false,
                Game.Network.Handling.PackageHandlerCatalog.Empty,
                NodeRole.Authority
            );
            using var controller = new GameWorldController(new WorldProjectActivity());
            var world = new WorldInfo
            {
                DirectoryName = rootName + ":/",
                ProjectFormatVersion = WorldVersions.ProjectFormatVersion
            };
            var exception = Assert.Throws<InvalidOperationException>(() =>
                controller.LoadAuthority(
                    new WorldLoadRequest(
                        world,
                        new SessionInfo { Target = SessionTarget.World },
                        node,
                        runtime,
                        DatabaseManager.GameDatabase,
                        WorldProjectPolicies.CaptureAuthority(new HostPreferences()),
                        new BlockTextureStore(directory),
                        false
                    )
                )
            );
            Assert.Equal("Storage root is read-only.", exception.Message);
            Assert.Null(controller.Runtime);
            Assert.Null(controller.WorldInfo);
            Assert.Empty(runtime.WorldCatalogs);
            Assert.Equal(original, File.ReadAllText(Path.Combine(directory, "Project.xml")));
            Assert.Single(Directory.EnumerateFileSystemEntries(directory));
        }
        finally
        {
            Storage.RegisterFileSystemRoot(rootName, AppContext.BaseDirectory, readOnly: true);
            Directory.Delete(directory, true);
        }
    }

    private sealed class MetadataMod : IMod
    {
        public void Configure(IModContext context) => context.Extensions.Register(
            BlockExtensions.RegistryName,
            ResourceId.Parse("test:new"),
            new BlockRegistration(static () => new MetadataBlock())
        );

        public void Start(IModContext context)
        {
        }

        public void Stop()
        {
        }
    }

    private sealed class MetadataBlock : AirBlock
    {
    }
}
