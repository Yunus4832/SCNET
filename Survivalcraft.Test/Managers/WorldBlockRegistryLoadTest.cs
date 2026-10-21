using Engine.Core;
using Engine.FileStorage;

using Game;
using Game.Blocks;
using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Widgets;

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
        var previousMode = RunMode.Value;
        var previousRuntime = CurrentModRuntime.Value;
        RunMode.Value = RunModeType.HeadlessServer;
        try
        {
            Dispatcher.Initialize();
            ContentManager.Initialize();
            using var runtime = GameModRuntime.Start(
                [
                    new ModDescriptor(new ModManifest("test", "Metadata", "1.0.0"), static () => new MetadataMod())
                ]
            );
            runtime.InitializeLanguage("zh-CN");
            runtime.InitializeContentData();
            CurrentModRuntime.Set(runtime);
            var world = new WorldInfo
            {
                DirectoryName = rootName + ":/",
                ProjectFormatVersion = WorldVersions.ProjectFormatVersion
            };
            var exception = Assert.Throws<InvalidOperationException>(() =>
                GameManager.LoadProject(world, new CanvasWidget())
            );
            Assert.Equal("Storage root is read-only.", exception.Message);
            Assert.Null(GameManager.Project);
            Assert.Null(runtime.WorldCatalog);
            Assert.Equal(original, File.ReadAllText(Path.Combine(directory, "Project.xml")));
            Assert.Single(Directory.EnumerateFileSystemEntries(directory));
        }
        finally
        {
            GameManager.DisposeProject();
            CurrentModRuntime.Set(previousRuntime);
            RunMode.Value = previousMode;
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
