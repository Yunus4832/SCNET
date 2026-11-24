using System.Xml.Linq;

using Engine.Core.FileStorage;
using Engine.Core.Runtime;

using EntitySystem.TemplatesDatabase;

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
    public async Task RegistryWriteFailurePreventsWorldConstructionAndReleasesCatalog()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scnet-block-registry-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var gameInfo = new XElement("Values", new XAttribute("Name", "GameInfo"));
        var values = new ValuesDictionary();
        new WorldSettings().Generator.Save(values);
        values.Save(gameInfo);
        var original = new XElement(
            "Project",
            new XAttribute("Version", WorldVersions.ProjectFormatVersion),
            new XElement("BlockRegistry"),
            new XElement("Subsystems", gameInfo)
        ).ToString();
        File.WriteAllText(Path.Combine(directory, "Project.xml"), original);
        const string rootName = "block-registry-write-test";
        Storage.RegisterFileSystemRoot(rootName, directory, readOnly: true);

        try
        {
            Dispatcher.Initialize();
            AssetManager.Initialize();
            using var runtime = GameModRuntime.Start(
                [
                    new ModDescriptor(new ModManifest("test", "Metadata", "1.0.0"), static () => new MetadataMod())
                ],
                side: RuntimeSide.Authority
            );
            runtime.InitializeContentData();
            var node = new NetNode(
                NetNodeOptions.Default,
                false,
                Game.Network.Handling.PackageHandlerCatalog.Empty,
                NodeRole.Authority
            );
            await using var controller = new GameWorldController(new WorldProjectActivity());
            var world = new WorldInfo
            {
                DirectoryName = rootName + ":/",
                ProjectFormatVersion = WorldVersions.ProjectFormatVersion
            };
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                controller.LoadAuthorityAsync(
                    new WorldLoadRequest(
                        world,
                        new SessionInfo { Target = SessionTarget.World },
                        node,
                        _ => runtime,
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

    [Fact]
    public void ReopeningDifferentWorldsKeepsPreviewAndReplicaBlockIdsIndependent()
    {
        Dispatcher.Initialize();
        AssetManager.Initialize();
        var descriptor = new ModDescriptor(
            new ModManifest("test", "Metadata", "1.0.0"),
            static () => new MetadataMod()
        );
        using var client = GameModRuntime.Start([descriptor]);
        client.InitializeContentData();
        var id = ResourceId.Parse("test:new");
        var preview = client.PreviewCatalog.GetBlock(id);
        var previewIndex = preview.BlockIndex;
        foreach (var savedIndex in new[] { 300, 410 })
        {
            using var server = GameModRuntime.Start([descriptor], side: RuntimeSide.Authority);
            server.InitializeContentData();
            var authority = new WorldRuntimeContext(RuntimeSide.Authority, false);
            var replica = new WorldRuntimeContext(RuntimeSide.Client, true);
            try
            {
                var saved = new BlockIndexMapping([new(id, savedIndex)]);
                var authoritative = server.CreateWorldCatalog(authority, saved);
                var replicated = client.CreateWorldCatalog(replica, authoritative.Registry.Mapping, true);
                Assert.Equal(savedIndex, authoritative.GetBlock(id).BlockIndex);
                Assert.Equal(savedIndex, replicated.GetBlock(id).BlockIndex);
                Assert.NotSame(authoritative.GetBlock(id), replicated.GetBlock(id));
                Assert.NotSame(preview, replicated.GetBlock(id));
                Assert.NotSame(client.Database, server.Database);
            }
            finally
            {
                replica.Close();
                authority.Close();
            }

            Assert.Empty(client.WorldCatalogs);
            Assert.Equal(previewIndex, client.PreviewCatalog.GetBlock(id).BlockIndex);
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
