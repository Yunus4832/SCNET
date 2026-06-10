using System.IO.Compression;
using System.Text;

using Game.Managers;
using Game.Modding;
using Game.Modding.Blocks;
using Game.Modding.Content;
using Game.Modding.Data;

namespace Survivalcraft.Test.Modding;

public class ModPackageTest
{
    [Fact]
    public void PackageLoadsEntrypointFromIsolatedAssembly()
    {
        using var packageStream = CreatePackage(
            """
            {
              "id": "example.test",
              "name": "Test Mod",
              "version": "1.0.0",
              "entrypoints": {
                "common": "Survivalcraft.Test.Modding.PackageEntrypointMod, Survivalcraft.Test"
              }
            }
            """,
            includeTestAssembly: true);
        var package = ModPackage.Read("test.scpak", packageStream);
        var host = new ModHost();

        host.LoadAndStart([package.CreateDescriptor(ModSide.Server)]);

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
            archive.CreateEntry("assemblies/Unused.dll");
        }

        stream.Position = 0;
        var exception = Assert.Throws<ModPackageException>(() => ModPackage.Read("missing.scpak", stream));

        Assert.Contains("manifest.json", exception.Message);
    }

    [Fact]
    public void PackageRejectsWrongRuntimeSide()
    {
        using var packageStream = CreatePackage("""
                                                {
                                                  "id": "example.client",
                                                  "name": "Client Mod",
                                                  "version": "1.0.0",
                                                  "side": "client",
                                                  "entrypoints": {
                                                    "client": "Survivalcraft.TestMod.TestMod, Survivalcraft.TestMod"
                                                  }
                                                }
                                                """);
        var package = ModPackage.Read("client.scpak", packageStream);

        var exception = Assert.Throws<ModPackageException>(() => package.CreateDescriptor(ModSide.Server));

        Assert.Contains("cannot load on Server", exception.Message);
    }

    [Fact]
    public void CatalogCreatesDependencyOrderedLoadPlan()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"scnet-mod-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            WritePackage(Path.Combine(directory, "addon.scpak"), Manifest("example.addon", "example.core"));
            WritePackage(Path.Combine(directory, "core.scpak"), Manifest("example.core"));
            File.WriteAllBytes(Path.Combine(directory, "legacy.scmod"), [1, 2, 3]);

            var plan = ModPackageCatalog.CreateLoadPlan(directory, ModSide.Server);

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
            new ModPackageSource("addon.scpak", () => new MemoryStream(addon, writable: false)),
            new ModPackageSource("core.scpak", () => new MemoryStream(core, writable: false))
        };

        var plan = ModPackageCatalog.CreateLoadPlan(sources, ModSide.Server);

        Assert.Equal(["example.core", "example.addon"], plan.Select(item => item.Manifest.Id));
        foreach (var descriptor in plan)
        {
            descriptor.Lifetime?.Dispose();
        }
    }

    [Fact]
    public void CatalogSkipsDisabledPackagesInLoadPlan()
    {
        var core = CreateDataPackage(Manifest("example.core")).ToArray();
        var addon = CreateDataPackage(Manifest("example.addon", "example.core")).ToArray();
        var sources = new[]
        {
            new ModPackageSource("addon.scpak", () => new MemoryStream(addon, writable: false)),
            new ModPackageSource("core.scpak", () => new MemoryStream(core, writable: false))
        };

        var plan = ModPackageCatalog.CreateLoadPlan(sources, ModSide.Server, ["example.addon"]);

        Assert.Equal(["example.core"], plan.Select(item => item.Manifest.Id));
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
                ["data/blocks/items.csv"] = "Type;DisplayName\nAirBlock;Data Air",
                ["data/database/entities.xdb"] = "<Patch />",
                ["data/recipes/items.cr"] = "<Recipes />"
            });
        var package = ModPackage.Read("data.scpak", packageStream);
        var descriptor = package.CreateDescriptor(ModSide.Server);
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
            });
        var package = ModPackage.Read("assets.scpak", packageStream);
        var host = new ModHost();
        host.LoadAndStart([package.CreateDescriptor(ModSide.Server)]);
        var catalog = ContentCatalog.Compile(host.Extensions);

        ContentManager.Initialize();
        catalog.Install();

        Assert.Equal("namespaced content", ContentManager.Get<string>("example.assets/text/readme"));
        Assert.Contains("zh-CN", catalog.LanguageTypes);
        catalog.InitializeLanguage("zh-CN");
        Assert.Equal("OK", LanguageManager.Ok);

        catalog.Uninstall();
        Assert.False(ContentManager.ContainsKey("example.assets/text/readme.txt"));
        host.StopAll();
    }

    [Fact]
    public void PackageRejectsAssetsOutsideItsNamespace()
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
                ["assets/other.mod/readme.txt"] = "not owned"
            });

        var exception = Assert.Throws<ModPackageException>(() => ModPackage.Read("assets.scpak", packageStream));

        Assert.Contains("must be inside assets/example.assets/", exception.Message);
    }

    [Fact]
    public void PackageHashIncludesContributionContents()
    {
        using var firstStream = CreatePackage(
            Manifest("example.hash"),
            new Dictionary<string, string> { ["data/blocks/items.csv"] = "first" });
        using var secondStream = CreatePackage(
            Manifest("example.hash"),
            new Dictionary<string, string> { ["data/blocks/items.csv"] = "second" });

        var first = ModPackage.Read("first.scpak", firstStream);
        var second = ModPackage.Read("second.scpak", secondStream);

        Assert.NotEqual(first.PackageHash, second.PackageHash);
    }

    private static MemoryStream CreatePackage(
        string manifest,
        IReadOnlyDictionary<string, string>? dataFiles = null,
        bool includeTestAssembly = false)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            var manifestEntry = archive.CreateEntry("manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open(), Encoding.UTF8, leaveOpen: false))
            {
                writer.Write(manifest);
            }

            if (includeTestAssembly)
            {
                var assemblyEntry = archive.CreateEntry("assemblies/Survivalcraft.Test.dll");
                using var entryStream = assemblyEntry.Open();
                using var assemblyStream = File.OpenRead(typeof(ModPackageTest).Assembly.Location);
                assemblyStream.CopyTo(entryStream);
            }

            if (dataFiles is not null)
            {
                foreach (var (path, content) in dataFiles)
                {
                    var dataEntry = archive.CreateEntry(path);
                    using var dataWriter = new StreamWriter(dataEntry.Open(), Encoding.UTF8, leaveOpen: false);
                    dataWriter.Write(content);
                }
            }
        }

        stream.Position = 0;
        return stream;
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
                ["data/blocks/marker.csv"] = "Type;DisplayName\nAirBlock;Marker"
            });
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
                   "side": "common",
                   "dependencies": {{dependencies}}
                 }
                 """;
    }
}

public sealed class PackageEntrypointMod : IMod
{
    public void Configure(IModContext context)
    {
        context.Extensions.Register(
            "markers",
            new ResourceId(context.Manifest.ModId, "loaded"),
            "configured");
    }

    public void Start(IModContext context)
    {
    }

    public void Stop()
    {
    }
}
