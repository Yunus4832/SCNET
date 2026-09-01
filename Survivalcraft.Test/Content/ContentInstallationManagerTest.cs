using System.Text;
using System.Text.Json;

using Content.Packaging;

using Engine.FileStorage;

using Game;
using Game.Content;
using Game.Managers;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Content;

[Collection(ConfigFileCollection.Name)]
public sealed class ContentInstallationManagerTest : IDisposable
{
    private readonly List<string> _installedWorlds = [];
    private readonly List<string> _temporaryDirectories = [];
    private readonly List<(ContentType Type, string Name)> _installedAssets = [];

    [Fact]
    public void ModInstallOnlyAcknowledgesValidatedCachedPackage()
    {
        using var package = ScpkgTestPackage.Create("""
                                                    {"id":"installer.example","name":"Installer Example","version":"1.0.0"}
                                                    """,
            new Dictionary<string, string> { ["data/marker.txt"] = "installed" });

        var result = ContentInstallationManager.Install(package);

        Assert.Null(result.AssetKey);
    }

    [Fact]
    public void WorldInstallWritesExpandedPayloadAsIndependentWorld()
    {
        using var package = CreateWorldPackage();

        var result = ContentInstallationManager.Install(package);
        var directory = Storage.CombinePaths(GamePaths.Worlds, result.AssetKey!);
        _installedWorlds.Add(directory);

        Assert.True(Storage.FileExists(Storage.CombinePaths(directory, "Project.xml")));
        Assert.True(Storage.FileExists(Storage.CombinePaths(directory, "Regions/0,0.dat")));
    }

    [Theory]
    [InlineData(ContentPackageType.BlocksTexture)]
    [InlineData(ContentPackageType.CharacterSkin)]
    public void ImageInstallAcceptsNonSeekablePayloadAndCreatesIndependentAssets(ContentPackageType type)
    {
        var directory = type == ContentPackageType.BlocksTexture ? GamePaths.BlockTextures : GamePaths.CharacterSkins;
        if (!Storage.DirectoryExists(directory))
        {
            Storage.CreateDirectory(directory);
        }

        using var first = CreateImagePackage(type);
        using var second = CreateImagePackage(type);

        var firstResult = ContentInstallationManager.Install(first);
        var secondResult = ContentInstallationManager.Install(second);
        var firstName = firstResult.AssetKey!;
        var secondName = secondResult.AssetKey!;
        var contentType = type == ContentPackageType.BlocksTexture
            ? ContentType.BlocksTexture
            : ContentType.CharacterSkin;
        _installedAssets.Add((contentType, firstName));
        _installedAssets.Add((contentType, secondName));

        Assert.NotEqual(firstName, secondName);
        Assert.NotEqual(firstResult.AssetKey, secondResult.AssetKey);
        Assert.Matches("^[0-9a-f]{32}$", firstName);
        if (type == ContentPackageType.BlocksTexture)
        {
            Assert.EndsWith(".png", BlocksTexturesManager.GetFileName(firstName), StringComparison.Ordinal);
        }
        else
        {
            Assert.True(CharacterSkinsManager.GetFileName(firstName, out var skinPath));
            Assert.EndsWith(".png", skinPath, StringComparison.Ordinal);
        }

        Assert.Equal("Installed Image", type == ContentPackageType.BlocksTexture
            ? BlocksTexturesManager.GetDisplayName(firstName)
            : CharacterSkinsManager.GetDisplayName(firstName));
    }

    [Fact]
    public void InvalidAssetImportLeavesNoTemporaryOrVisibleAsset()
    {
        if (!Storage.DirectoryExists(GamePaths.BlockTextures))
        {
            Storage.CreateDirectory(GamePaths.BlockTextures);
        }

        var before = Storage.ListFileNames(GamePaths.BlockTextures).ToHashSet(StringComparer.Ordinal);
        using var invalid = new MemoryStream("not an image"u8.ToArray());

        Assert.ThrowsAny<Exception>(() => BlocksTexturesManager.ImportBlocksTexture("Broken", invalid));

        Assert.Equal(before, Storage.ListFileNames(GamePaths.BlockTextures).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void ImageReplacementPreservesAssetKeyAndUpdatesDisplayName()
    {
        if (!Storage.DirectoryExists(GamePaths.BlockTextures))
        {
            Storage.CreateDirectory(GamePaths.BlockTextures);
        }

        using var original = CreateImagePackage(ContentPackageType.BlocksTexture);
        var created = ContentInstallationManager.Install(original);
        _installedAssets.Add((ContentType.BlocksTexture, created.AssetKey!));
        using var replacement = CreateImagePackage(ContentPackageType.BlocksTexture, "Replacement Image");

        var replaced = ContentInstallationManager.Install(replacement, new ContentInstallOptions(created.AssetKey));

        Assert.Equal(created.AssetKey, replaced.AssetKey);
        Assert.Equal("Replacement Image", BlocksTexturesManager.GetDisplayName(replaced.AssetKey!));
    }

    [Fact]
    public void WorldReplacementPreservesAssetKeyAndReplacesPayload()
    {
        using var original = CreateWorldPackage("first");
        var created = ContentInstallationManager.Install(original);
        var directory = Storage.CombinePaths(GamePaths.Worlds, created.AssetKey!);
        _installedWorlds.Add(directory);
        using var replacement = CreateWorldPackage("second");

        var replaced = ContentInstallationManager.Install(replacement, new ContentInstallOptions(created.AssetKey));

        Assert.Equal(created.AssetKey, replaced.AssetKey);
        using var region = Storage.OpenFile(Storage.CombinePaths(directory, "Regions/0,0.dat"), OpenFileMode.Read);
        using var reader = new StreamReader(region);
        Assert.Equal("second", reader.ReadToEnd());
    }

    [Fact]
    public void AssetReferenceReplacementUpdatesPersistedWorldByAssetKey()
    {
        if (!Storage.DirectoryExists(GamePaths.Worlds))
        {
            Storage.CreateDirectory(GamePaths.Worlds);
        }

        var directory = Storage.CombinePaths(GamePaths.Worlds, $"ReferenceTest{Guid.NewGuid():N}");
        Storage.CreateDirectory(directory);
        _installedWorlds.Add(directory);
        var project = Storage.CombinePaths(directory, "Project.xml");
        using (var output = Storage.OpenFile(project, OpenFileMode.Create))
        using (var writer = new StreamWriter(output))
        {
            writer.Write("<Project><Value Name=\"BlockTextureName\" Value=\"old-key\" /></Project>");
        }

        var count = WorldsManager.ReplaceAssetReferences(ContentType.BlocksTexture, "old-key", "new-key");

        Assert.Equal(1, count);
        using var input = Storage.OpenFile(project, OpenFileMode.Read);
        using var reader = new StreamReader(input);
        Assert.Contains("Value=\"new-key\"", reader.ReadToEnd());
    }

    [Fact]
    public void ImageCreationProducesTemporaryValidatedPackageAndBaselineVersionKeepsIdentifier()
    {
        using var source = new MemoryStream(OnePixelPng(), writable: false);
        using var first = ContentPackageCreationManager.CreateImage(ContentPackageType.BlocksTexture,
            new ContentCreationIdentity("Created Texture", "1.0.0"), source);
        using var firstPackage = first.OpenRead();
        var firstInspection = ContentPackageReader.Inspect(firstPackage);
        firstPackage.Position = 0;
        using var nextSource = new MemoryStream(OnePixelPng(), writable: false);
        using var next = ContentPackageCreationManager.CreateImage(ContentPackageType.BlocksTexture,
            new ContentCreationIdentity("Created Texture v2", "2.0.0", firstPackage), nextSource);
        using var nextPackage = next.OpenRead();
        var nextInspection = ContentPackageReader.Inspect(nextPackage);

        Assert.Equal(ContentPackageType.BlocksTexture, firstInspection.Manifest.Type);
        Assert.Equal(firstInspection.Manifest.Identifier, nextInspection.Manifest.Identifier);
        Assert.Equal("2.0.0", nextInspection.Manifest.Version);
        Assert.NotEqual(first.PackageHash, next.PackageHash);
    }

    [Fact]
    public void ImageCreationRejectsNonPngAndCleansTemporaryInput()
    {
        if (!Storage.DirectoryExists(GamePaths.ContentPackageCreationTemp))
        {
            Storage.CreateDirectory(GamePaths.ContentPackageCreationTemp);
        }

        var before = Storage.ListFileNames(GamePaths.ContentPackageCreationTemp)
            .ToHashSet(StringComparer.Ordinal);
        using var gif = new MemoryStream("GIF89a"u8.ToArray(), writable: false);

        Assert.ThrowsAny<Exception>(() => ContentPackageCreationManager.CreateImage(
            ContentPackageType.CharacterSkin, new ContentCreationIdentity("Not PNG", "1.0.0"), gif));

        Assert.Equal(before, Storage.ListFileNames(GamePaths.ContentPackageCreationTemp)
            .ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public async Task LargeWorldManufactureCacheAndInstallRemainStreaming()
    {
        const int regionSize = 8 * 1024 * 1024;
        if (!Storage.DirectoryExists(GamePaths.Worlds))
        {
            Storage.CreateDirectory(GamePaths.Worlds);
        }

        var sourceDirectory = Storage.CombinePaths(GamePaths.Worlds, $"LargeSource{Guid.NewGuid():N}");
        var regions = Storage.CombinePaths(sourceDirectory, "Regions");
        Storage.CreateDirectory(regions);
        _installedWorlds.Add(sourceDirectory);
        using (var output = Storage.OpenFile(Storage.CombinePaths(sourceDirectory, "Project.xml"), OpenFileMode.Create))
        using (var writer = new StreamWriter(output))
        {
            writer.Write(
                "<Project Version=\"SCNET-1\" Guid=\"9e9a67f8-79df-4d05-8cfa-61bd8095661e\" Name=\"GameProject\"><Subsystems /><Entities /></Project>");
        }

        var random = new Random(42);
        var buffer = new byte[64 * 1024];
        using (var output = Storage.OpenFile(Storage.CombinePaths(regions, "0,0.dat"), OpenFileMode.Create))
        {
            for (var remaining = regionSize; remaining > 0; remaining -= buffer.Length)
            {
                random.NextBytes(buffer);
                output.Write(buffer, 0, Math.Min(buffer.Length, remaining));
            }
        }

        using var artifact = ContentPackageCreationManager.CreateWorld(
            new ContentCreationIdentity("Large World", "1.0.0"), Storage.GetFileName(sourceDirectory));
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"scnet-large-cache-{Guid.NewGuid():N}");
        _temporaryDirectories.Add(cacheDirectory);
        var cache = new ContentPackageCache(cacheDirectory);
        using var artifactStream = artifact.OpenRead();
        using var tracking = new ReadTrackingStream(artifactStream);
        var cached = await cache.ImportAsync(tracking);
        using var package = cache.OpenValidated(cached.PackageHash);

        var installed = ContentInstallationManager.Install(package);
        var installedDirectory = Storage.CombinePaths(GamePaths.Worlds, installed.AssetKey!);
        _installedWorlds.Add(installedDirectory);

        Assert.True(tracking.MaximumReadSize <= 64 * 1024);
        Assert.Equal(regionSize, Storage.GetFileSize(Storage.CombinePaths(installedDirectory, "Regions/0,0.dat")));
    }

    [Fact]
    public async Task LocalImportAndDownloadedCacheUseSameInstallationWorkflow()
    {
        if (!Storage.DirectoryExists(GamePaths.BlockTextures))
        {
            Storage.CreateDirectory(GamePaths.BlockTextures);
        }

        var localCachePath = Path.Combine(Path.GetTempPath(), $"scnet-local-flow-{Guid.NewGuid():N}");
        var remoteCachePath = Path.Combine(Path.GetTempPath(), $"scnet-remote-flow-{Guid.NewGuid():N}");
        _temporaryDirectories.Add(localCachePath);
        _temporaryDirectories.Add(remoteCachePath);
        var localCache = new ContentPackageCache(localCachePath);
        var remoteCache = new ContentPackageCache(remoteCachePath);
        using var localSource = CreateImagePackage(ContentPackageType.BlocksTexture, "Workflow Texture");
        var local = await ContentPackageWorkflow.ImportAndInstallAsync(localSource, localCache);
        _installedAssets.Add((ContentType.BlocksTexture, local.Installation.AssetKey!));
        using var remoteSource = CreateImagePackage(ContentPackageType.BlocksTexture, "Workflow Texture");
        var remoteEntry = await remoteCache.ImportAsync(remoteSource);
        var remote = ContentPackageWorkflow.InstallCached(remoteCache, remoteEntry.PackageHash);
        _installedAssets.Add((ContentType.BlocksTexture, remote.AssetKey!));

        Assert.Equal(local.Installation.Type, remote.Type);
        Assert.Equal(local.Installation.DisplayName, remote.DisplayName);
        Assert.Equal("Workflow Texture", BlocksTexturesManager.GetDisplayName(local.Installation.AssetKey!));
        Assert.Equal("Workflow Texture", BlocksTexturesManager.GetDisplayName(remote.AssetKey!));
    }

    public void Dispose()
    {
        foreach (var installedWorld in _installedWorlds)
        {
            if (Storage.DirectoryExists(installedWorld))
            {
                WorldsManager.DeleteWorld(installedWorld);
            }
        }

        foreach (var (type, name) in _installedAssets)
        {
            ContentPackageManager.DeleteContent(type, name);
        }

        foreach (var directory in _temporaryDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static MemoryStream CreateWorldPackage(string regionMarker = "region")
    {
        var metadata = JsonSerializer.SerializeToElement(new Dictionary<string, object>
        {
            ["projectFormat"] = "scnet-project-xml-v1",
            ["regionsDirectory"] = "payload/world/Regions"
        });
        var manifest = new ContentPackageManifest(1, ContentPackageType.World,
            "9e9a67f8-79df-4d05-8cfa-61bd8095661e", "Installed World", "1.0.0",
            new ContentPackagePayload("scnet.world-v1", "payload/world/Project.xml", "application/xml"), metadata);
        var project = Encoding.UTF8.GetBytes(
            "<Project Version=\"SCNET-1\" Guid=\"9e9a67f8-79df-4d05-8cfa-61bd8095661e\" Name=\"GameProject\"><Subsystems /><Entities /></Project>");
        var region = Encoding.UTF8.GetBytes(regionMarker);
        var output = new MemoryStream();
        ContentPackageWriter.Write(output, manifest,
        [
            new ContentPackageWriteEntry("payload/world/Project.xml", project.Length,
                () => new MemoryStream(project, writable: false)),
            new ContentPackageWriteEntry("payload/world/Regions/0,0.dat", region.Length,
                () => new MemoryStream(region, writable: false))
        ]);
        output.Position = 0;
        return output;
    }

    private static MemoryStream CreateImagePackage(ContentPackageType type, string displayName = "Installed Image")
    {
        var bytes = OnePixelPng();
        var isTexture = type == ContentPackageType.BlocksTexture;
        var entry = isTexture ? "payload/texture.png" : "payload/skin.png";
        var manifest = new ContentPackageManifest(1, type, Guid.NewGuid().ToString(), displayName, "1.0.0",
            new ContentPackagePayload(isTexture ? "scnet.blocks-texture.png-v1" : "scnet.character-skin.png-v1",
                entry, "image/png"),
            JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["width"] = 1, ["height"] = 1 }));
        var output = new MemoryStream();
        ContentPackageWriter.Write(output, manifest,
            [new ContentPackageWriteEntry(entry, bytes.Length, () => new MemoryStream(bytes, writable: false))]);
        output.Position = 0;
        return output;
    }

    private static byte[] OnePixelPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR4nGP4DwQACfsD/fteaysAAAAASUVORK5CYII=");

    private sealed class ReadTrackingStream(Stream inner) : Stream
    {
        public int MaximumReadSize { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            MaximumReadSize = Math.Max(MaximumReadSize, count);
            return inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            MaximumReadSize = Math.Max(MaximumReadSize, buffer.Length);
            return inner.Read(buffer);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            MaximumReadSize = Math.Max(MaximumReadSize, buffer.Length);
            return inner.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
