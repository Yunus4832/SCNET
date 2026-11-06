using System.IO.Compression;

using Content.Packaging;

using EntitySystem.Core;

using Game.ClientContent;
using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Modding.Content;
using Game.Modding.Data;
using Game.World;

namespace Survivalcraft.Test.Modding;

[Collection(ConfigFileCollection.Name)]
public class ModPackageTest
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnePackagedDefinitionRunsAuthorityBusinessInEitherHostWithIndependentWorldState(bool hasPresentation)
    {
        using var stream = CreatePackage(
            """
            {
              "id": "example.test",
              "name": "Test Mod",
              "version": "1.0.0",
              "entrypoint": "Survivalcraft.Test.Modding.PackageEntrypointMod, Survivalcraft.Test"
            }
            """,
            includeTestAssembly: true
        );
        var package = ModPackage.Read("definition.scpkg", stream);
        var host = new ModHost();
        host.LoadAndStart([package.CreateDescriptor()]);
        Assert.True(
            host.Extensions.GetRegistry<Dictionary<string, int>>("fixture_counts")
                .TryGet(new ResourceId(new ModId("example.test"), "counts"), out var counts)
        );
        using (var authority = new Project())
        using (var client = new Project())
        {
            new WorldRuntimeContext(RuntimeSide.Authority, false).BindProject(authority);
            new WorldRuntimeContext(
                RuntimeSide.Client,
                (hasPresentation)
            ).BindProject(client);
            host.Gameplay.Invoke(new WorldUpdatingContext(authority, 1));
            host.Gameplay.Invoke(new WorldUpdatingContext(client, 1));
            host.Gameplay.Invoke(new WorldUpdatingContext(authority, 1));
            Assert.Equal(2, authority.ExtensionData.Get("example.test", "fixture").GetValue<int>("updates"));
            Assert.Empty(client.ExtensionData.Save());
            Assert.Equal(1, counts!["configure"]);
            Assert.Equal(1, counts["start"]);
            Assert.Equal(2, counts["authority"]);
            Assert.Equal(1, counts["last_client_state"]);
            Assert.Equal(hasPresentation ? 1 : 0, counts["presentation"]);
        }

        Assert.Equal(2, counts!["disposed"]);
        host.StopAll();
    }

    [Fact]
    public void PackageLoadsEntrypointFromIsolatedAssembly()
    {
        using var packageStream = CreatePackage(
            """
            {
              "id": "example.test",
              "name": "Test Mod",
              "version": "1.0.0",
              "entrypoint": "Survivalcraft.Test.Modding.PackageEntrypointMod, Survivalcraft.Test"
            }
            """,
            includeTestAssembly: true
        );
        var package = ModPackage.Read("test.scpkg", packageStream);
        var host = new ModHost();

        host.LoadAndStart([package.CreateDescriptor()]);

        var markerId = new ResourceId(new ModId("example.test"), "loaded");
        Assert.True(host.Extensions.GetRegistry<string>("markers").TryGet(markerId, out var marker));
        Assert.Equal("configured", marker);
        Assert.Equal(ModState.Started, Assert.Single(host.Runtimes).State);

        host.StopAll();
        Assert.False(host.Extensions.GetRegistry<string>("markers").TryGet(markerId, out _));
    }

    [Fact]
    public void PackageRejectsMissingManifest()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            archive.CreateEntry("payload/mod.json");
        }

        stream.Position = 0;
        var exception = Assert.Throws<ModPackageException>(() => ModPackage.Read("missing.scpkg", stream));

        Assert.Contains("manifest.json", exception.Message);
    }

    [Fact]
    public void CatalogCreatesDependencyOrderedLoadPlan()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scnet-mod-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            WritePackage(Path.Combine(directory, "addon.scpkg"), Manifest("example.addon", "example.core"));
            WritePackage(Path.Combine(directory, "core.scpkg"), Manifest("example.core"));
            File.WriteAllBytes(Path.Combine(directory, "legacy.scmod"), [1, 2, 3]);

            var plan = ModPackageCatalog.CreateLoadPlan(directory);

            Assert.Equal(["example.core", "example.addon"], plan.Select(item => item.Manifest.Id));
            foreach (var descriptor in plan)
            {
                descriptor.Lifetime?.Dispose();
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void CatalogLoadsFromAbstractPackageSources()
    {
        var core = CreateDataPackage(Manifest("example.core")).ToArray();
        var addon = CreateDataPackage(Manifest("example.addon", "example.core")).ToArray();
        var sources = new[]
        {
            new ModPackageSource("addon.scpkg", () => new MemoryStream(addon, writable: false)),
            new ModPackageSource("core.scpkg", () => new MemoryStream(core, writable: false))
        };

        var plan = ModPackageCatalog.CreateLoadPlan(sources);

        Assert.Equal(["example.core", "example.addon"], plan.Select(item => item.Manifest.Id));
        foreach (var descriptor in plan)
        {
            descriptor.Lifetime?.Dispose();
        }
    }

    [Fact]
    public void DataOnlyPackageRegistersStandardDataDirectories()
    {
        using var packageStream = CreatePackage(
            """
            {
              "id": "example.data",
              "name": "Data Mod",
              "version": "1.0.0"
            }
            """,
            new Dictionary<string, string>
            {
                ["data/blocks/items.csv"] = "Id;DisplayName\ngame:air;Data Air",
                ["data/database/entities.xdb"] = "<Patch />",
                ["data/recipes/items.cr"] = "<Recipes />"
            }
        );
        var package = ModPackage.Read("data.scpkg", packageStream);
        var descriptor = package.CreateDescriptor();
        var host = new ModHost();

        host.LoadAndStart([descriptor]);

        Assert.Single(host.Extensions.GetRegistry<BlockDataRegistration>(BlockExtensions.DataRegistryName).Entries);
        Assert.Single(host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.DatabaseRegistryName).Entries);
        Assert.Single(host.Extensions.GetRegistry<XmlDataRegistration>(XmlDataExtensions.RecipeRegistryName).Entries);
        host.StopAll();
    }

    [Fact]
    public void AssetOnlyPackageInstallsNamespacedContentAndUninstallsIt()
    {
        using var packageStream = CreatePackage(
            """
            {
              "id": "example.assets",
              "name": "Asset Mod",
              "version": "1.0.0"
            }
            """,
            new Dictionary<string, string>
            {
                ["assets/example.assets/text/readme.txt"] = "namespaced content",
                ["assets/example.assets/lang/zh-CN.json"] = "{\"Usual\":{\"ok\":\"OK\"}}"
            }
        );
        var package = ModPackage.Read("assets.scpkg", packageStream);
        var host = new ModHost();
        var gameId = new ResourceId(new ModId("game"), "lang/zh-CN.json");
        using var builtInLanguage = host.Extensions.GetRegistry<ContentRegistration>(ContentExtensions.RegistryName)
            .Register(
                gameId.Namespace,
                gameId,
                new ContentRegistration(
                    "lang/zh-CN.json",
                    """{"Language":{"Name":"中文"},"Usual":{"ok":"确定"}}"""u8.ToArray()
                )
            );
        host.LoadAndStart([package.CreateDescriptor()]);
        var catalog = ContentCatalog.Compile(host.Extensions);

        ContentManager.Initialize();
        using (var client = new ClientContentRuntime([]))
        {
            client.InstallResources(catalog.RegisteredResources);
            Assert.Equal("namespaced content", ContentManager.Get<string>("example.assets/text/readme"));
            Assert.Contains("zh-CN", catalog.LanguageTypes);
            client.PublishLanguage(catalog.CreateLanguageCatalog("zh-CN"));
            Assert.Equal("OK", Game.Localization.ClientLanguage.Catalog.Ok);
            Assert.Equal("中文", Game.Localization.ClientLanguage.Catalog.GetLanguageDisplayName("zh-CN"));
        }

        Assert.False(ContentManager.ContainsKey("example.assets/text/readme.txt"));
        host.StopAll();
    }

    [Fact]
    public void WriterRejectsAssetsOutsideModNamespace()
    {
        var exception = Assert.Throws<ContentPackageException>(() => CreatePackage(
                """
                {
                  "id": "example.assets",
                  "name": "Asset Mod",
                  "version": "1.0.0",
                  "entrypoint": "Example.ModEntry, Example"
                }
                """,
                new Dictionary<string, string>
                {
                    ["assets/other.mod/readme.txt"] = "not owned"
                }
            )
        );

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackageHashIncludesContributionContents()
    {
        using var firstStream = CreatePackage(
            Manifest("example.hash"),
            new Dictionary<string, string> { ["data/blocks/items.csv"] = "first" }
        );
        using var secondStream = CreatePackage(
            Manifest("example.hash"),
            new Dictionary<string, string> { ["data/blocks/items.csv"] = "second" }
        );

        var first = ModPackage.Read("first.scpkg", firstStream);
        var second = ModPackage.Read("second.scpkg", secondStream);

        Assert.NotEqual(first.PackageHash, second.PackageHash);
    }

    [Fact]
    public void PackageHashIgnoresZipEntryOrder()
    {
        using var firstStream = CreatePackage(
            Manifest("example.order"),
            new Dictionary<string, string>
            {
                ["data/blocks/a.csv"] = "A",
                ["data/blocks/b.csv"] = "B"
            }
        );
        using var secondStream = CreatePackage(
            Manifest("example.order"),
            new Dictionary<string, string>
            {
                ["data/blocks/b.csv"] = "B",
                ["data/blocks/a.csv"] = "A"
            }
        );

        var first = ModPackage.Read("first.scpkg", firstStream);
        var second = ModPackage.Read("second.scpkg", secondStream);

        Assert.Equal(first.PackageHash, second.PackageHash);
    }

    private static MemoryStream CreatePackage(
        string manifest,
        IReadOnlyDictionary<string, string>? dataFiles = null,
        bool includeTestAssembly = false
    )
    {
        return ScpkgTestPackage.Create(
            manifest,
            dataFiles,
            includeTestAssembly ? typeof(ModPackageTest).Assembly.Location : null
        );
    }

    private static void WritePackage(string path, string manifest)
    {
        using var package = CreateDataPackage(manifest);
        using var file = File.Create(path);
        package.CopyTo(file);
    }

    private static MemoryStream CreateDataPackage(string manifest)
    {
        return CreatePackage(
            manifest,
            new Dictionary<string, string>
            {
                ["data/blocks/marker.csv"] = "Id;DisplayName\ngame:air;Marker"
            }
        );
    }

    private static string Manifest(string id, string? dependency = null)
    {
        var dependencies = dependency is null
            ? "[]"
            : $"[{{ \"id\": \"{dependency}\" }}]";
        return $$"""
                 {
                   "id": "{{id}}",
                   "name": "{{id}}",
                   "version": "1.0.0",

                   "dependencies": {{dependencies}}
                 }
                 """;
    }
}

public sealed class PackageEntrypointMod : IMod
{
    // Fixture telemetry is shared; the simulated business counter belongs to each world state.
    private readonly Dictionary<string, int> _counts = new()
    {
        ["configure"] = 0, ["start"] = 0, ["authority"] = 0, ["presentation"] = 0, ["disposed"] = 0
    };

    public void Configure(IModContext context)
    {
        _counts["configure"]++;
        context.Extensions.Register("fixture_counts", new ResourceId(context.Manifest.ModId, "counts"), _counts);
        var states = context.Worlds.Register(
            new ResourceId(context.Manifest.ModId, "fixture_state"),
            _ => new FixtureState(() => _counts["disposed"]++)
        );
        context.Gameplay.OnWorldUpdating(update =>
            {
                var runtime = WorldRuntimeContext.For(update.Project);
                var state = states.Get(runtime);
                state.Updates++;
                if (runtime.IsAuthority)
                {
                    context.GetWorldData(update.Project, "fixture").SetValue("updates", state.Updates);
                    _counts["authority"]++;
                }
                else { _counts["last_client_state"] = state.Updates; }

                if (runtime.HasPresentation) { _counts["presentation"]++; }
            }
        );
        context.Extensions.Register(
            "markers",
            new ResourceId(context.Manifest.ModId, "loaded"),
            "configured"
        );
    }

    public void Start(IModContext context)
    {
        _counts["start"]++;
    }

    public void Stop()
    {
    }

    private sealed class FixtureState(Action dispose) : IDisposable
    {
        public int Updates { get; set; }

        public void Dispose() => dispose();
    }
}
