using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using Content.Packaging;

using Engine.Core.FileStorage;
using Engine.Media.Images;

using Game.Characters;
using Game.Content;
using Game.FileStorage;
using Game.Managers;
using Game.Subsystems;
using Game.World;

using Survivalcraft.Test.Fixtures;
using Survivalcraft.Test.Modding;

using Color = Engine.Core.Mathematics.Color;

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
        using var package = ScpkgTestPackage.Create(
            """
            {"id":"installer.example","name":"Installer Example","version":"1.0.0"}
            """,
            new Dictionary<string, string> { ["data/marker.txt"] = "installed" }
        );

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
    [InlineData("replace", false)]
    [InlineData("replace", true)]
    [InlineData("delete", false)]
    [InlineData("delete", true)]
    [InlineData("restore", false)]
    [InlineData("restore", true)]
    public void WorldFilesRemainProtectedWhileTheAuthorityOwnsThem(string operation, bool closed)
    {
        using var package = CreateWorldPackage("protected");
        var installed = ContentInstallationManager.Install(package);
        var directory = Storage.CombinePaths(GamePaths.Worlds, installed.AssetKey!);
        _installedWorlds.Add(directory);
        WithSplitWorlds((authority, _) =>
            {
                authority.FindSubsystem<SubsystemGameInfo>(true)!.DirectoryName = directory;
                GameManager.WorldActivity.Track(WorldRuntimeContext.For(authority), directory);
                if (closed) { WorldRuntimeContext.For(authority).Close(); }

                using var replacement = CreateWorldPackage("replacement");
                if (operation == "restore")
                {
                    using var output = Storage.OpenFile(
                        Storage.CombinePaths(directory, "Guard.snapshot"),
                        OpenFileMode.Create
                    );
                    using var archive = new ZipArchive(output, ZipArchiveMode.Create);
                }

                Action modify = operation switch
                {
                    "replace" => () => ContentInstallationManager.Install(
                        replacement,
                        new ContentInstallOptions(installed.AssetKey)
                    ),
                    "delete" => () => Game.Application.ClientApplication.Worlds.DeleteWorld(directory),
                    "restore" => () =>
                        Game.Application.ClientApplication.Worlds.RestoreWorldFromSnapshot(directory, "Guard"),
                    _ => throw new ArgumentOutOfRangeException(nameof(operation))
                };
                Assert.Throws<InvalidOperationException>(modify);
                using var region = Storage.OpenFile(
                    Storage.CombinePaths(directory, "Regions/0,0.dat"),
                    OpenFileMode.Read
                );
                using var reader = new StreamReader(region);
                Assert.Equal("protected", reader.ReadToEnd());
            }
        );
    }

    [Theory]
    [InlineData("dot", false)]
    [InlineData("dot", true)]
    [InlineData("ancestor", false)]
    [InlineData("ancestor", true)]
    [InlineData("descendant", false)]
    [InlineData("descendant", true)]
    public void OwnedWorldCannotBeRemovedThroughAnAliasOrContainingDirectory(string pathKind, bool restore)
    {
        using var package = CreateWorldPackage("protected");
        var installed = ContentInstallationManager.Install(package);
        var directory = Storage.CombinePaths(GamePaths.Worlds, installed.AssetKey!);
        _installedWorlds.Add(directory);
        WithSplitWorlds((authority, _) =>
            {
                var owned = pathKind == "ancestor" ? Storage.CombinePaths(directory, "Nested") : directory;
                Storage.CreateDirectory(owned);
                authority.FindSubsystem<SubsystemGameInfo>(true)!.DirectoryName = owned;
                GameManager.WorldActivity.Track(WorldRuntimeContext.For(authority), owned);
                var target = pathKind switch
                {
                    "dot" => directory + "/.",
                    "descendant" => Storage.CombinePaths(directory, "Regions"),
                    _ => directory
                };
                if (restore)
                {
                    using var output = Storage.OpenFile(
                        Storage.CombinePaths(target, "Guard.snapshot"),
                        OpenFileMode.Create
                    );
                    using var archive = new ZipArchive(output, ZipArchiveMode.Create);
                }

                Assert.Throws<InvalidOperationException>(() =>
                    {
                        if (restore)
                        {
                            Game.Application.ClientApplication.Worlds.RestoreWorldFromSnapshot(target, "Guard");
                        }
                        else { Game.Application.ClientApplication.Worlds.DeleteWorld(target); }
                    }
                );
                Assert.True(Storage.FileExists(Storage.CombinePaths(directory, "Regions/0,0.dat")));
            }
        );
    }

    [Fact]
    public void AnInactiveSiblingCanBeReplacedWhileAnotherWorldIsOwned()
    {
        using var firstPackage = CreateWorldPackage("active");
        using var secondPackage = CreateWorldPackage("inactive");
        var first = ContentInstallationManager.Install(firstPackage);
        var second = ContentInstallationManager.Install(secondPackage);
        var active = Storage.CombinePaths(GamePaths.Worlds, first.AssetKey!);
        var inactive = Storage.CombinePaths(GamePaths.Worlds, second.AssetKey!);
        _installedWorlds.Add(active);
        _installedWorlds.Add(inactive);
        WithSplitWorlds((authority, _) =>
            {
                authority.FindSubsystem<SubsystemGameInfo>(true)!.DirectoryName = active;
                GameManager.WorldActivity.Track(WorldRuntimeContext.For(authority), active);
                using var replacement = CreateWorldPackage("replaced");
                ContentInstallationManager.Install(replacement, new ContentInstallOptions(second.AssetKey));
                using var input = Storage.OpenFile(
                    Storage.CombinePaths(inactive, "Regions/0,0.dat"),
                    OpenFileMode.Read
                );
                using var reader = new StreamReader(input);
                Assert.Equal("replaced", reader.ReadToEnd());
                Assert.True(Storage.FileExists(Storage.CombinePaths(active, "Regions/0,0.dat")));
            }
        );
    }

    [Theory]
    [InlineData(ContentType.BlocksTexture)]
    [InlineData(ContentType.CharacterSkin)]
    public void AssetReferenceReplacementUpdatesBothWorldsBeforeTheirNextSave(ContentType type)
    {
        Storage.CreateDirectory(GamePaths.Worlds);
        WithSplitWorlds((authority, client) =>
            {
                GameManager.WorldActivity.Track(WorldRuntimeContext.For(authority), null);
                foreach (var project in new[] { authority, client })
                {
                    project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.BlocksTextureName = "old-key";
                    var characters = new SubsystemCharacters();
                    project.AddSubsystem(characters);
                    var character = (CharacterData)RuntimeHelpers.GetUninitializedObject(typeof(CharacterData));
                    character.CharacterSkinName = "old-key";
                    ((List<CharacterData>)typeof(SubsystemCharacters).GetField(
                            "_characters",
                            BindingFlags.Instance | BindingFlags.NonPublic
                        )!
                        .GetValue(characters)!).Add(character);
                }

                Game.Application.ClientApplication.Worlds.ReplaceAssetReferences(type, "old-key", "new-key");

                foreach (var project in new[] { authority, client })
                {
                    if (type == ContentType.BlocksTexture)
                    {
                        Assert.Equal(
                            "new-key",
                            project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings.BlocksTextureName
                        );
                    }
                    else
                    {
                        Assert.Equal(
                            "new-key",
                            Assert.Single(project.FindSubsystem<SubsystemCharacters>(true)!.Characters)
                                .CharacterSkinName
                        );
                    }
                }
            }
        );
    }

    private static void WithSplitWorlds(Action<RuntimeTestProject, RuntimeTestProject> action)
    {
        using var session = new GameRuntime(false);
        using var authority = new RuntimeTestProject(RuntimeSide.Authority);
        using var client = new RuntimeTestProject(RuntimeSide.Client);
        authority.AddSubsystem(new SubsystemGameInfo { WorldSettings = new WorldSettings() });
        client.AddSubsystem(new SubsystemGameInfo { WorldSettings = new WorldSettings() });
        session.Attach(WorldRuntimeContext.For(authority));
        session.Attach(WorldRuntimeContext.For(client));

        GameManager.WorldActivity.Track(WorldRuntimeContext.For(client), null);
        try
        {
            action(authority, client);
        }
        finally
        {
            foreach (var project in new[] { authority, client })
            {
                if (project.FindSubsystem<SubsystemCharacters>() is { } characters)
                {
                    ((List<CharacterData>)typeof(SubsystemCharacters).GetField(
                            "_characters",
                            BindingFlags.Instance | BindingFlags.NonPublic
                        )!
                        .GetValue(characters)!).Clear();
                }
            }
        }
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
            Assert.True(CharacterSkinsManager.CreateStore().TryGetFileName(firstName, out var skinPath));
            Assert.EndsWith(".png", skinPath, StringComparison.Ordinal);
        }

        Assert.Equal(
            "Installed Image",
            type == ContentPackageType.BlocksTexture
                ? BlocksTexturesManager.GetDisplayName(firstName)
                : CharacterSkinsManager.GetDisplayName(firstName)
        );
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

        var count = Game.Application.ClientApplication.Worlds.ReplaceAssetReferences(
            ContentType.BlocksTexture,
            "old-key",
            "new-key"
        );

        Assert.Equal(1, count);
        using var input = Storage.OpenFile(project, OpenFileMode.Read);
        using var reader = new StreamReader(input);
        Assert.Contains("Value=\"new-key\"", reader.ReadToEnd());
    }

    [Theory]
    [InlineData(ImageFormat.Png)]
    [InlineData(ImageFormat.WebP)]
    public void NetworkSkinStorageAndRepliesPreserveTheReceivedEncoding(ImageFormat format)
    {
        using var image = new ImageBuffer(16, 16);
        Array.Fill(image.Pixels, new Color(200, 100, 50, 128));
        var encoded = ImageCodec.Encode(image, format);
        var name = Guid.NewGuid().ToString("N");
        CharacterSkinsManager.CreateStore().Save(name, encoded);
        _installedAssets.Add((ContentType.CharacterSkin, name));
        Assert.True(CharacterSkinsManager.CreateStore().TryGetFileName(name, out var path));
        Assert.EndsWith(format == ImageFormat.WebP ? ".webp" : ".png", path, StringComparison.Ordinal);
        var reply = Assert.IsType<SkinReplyPackage>(
            ClothingPackageBuilder.Capture(
                CharacterSkinsManager.CreateStore(),
                name,
                ComponentClothingPackage.DataType.ReplySkin
            )
        );
        Assert.Equal(encoded, reply.SkinData);
    }

    [Theory]
    [InlineData(ContentPackageType.BlocksTexture)]
    [InlineData(ContentPackageType.CharacterSkin)]
    public void ReplacingAnImageCanChangeEncodingWithoutChangingItsBytesOrKey(ContentPackageType type)
    {
        using var image = new ImageBuffer(16, 16);
        Array.Fill(image.Pixels, new Color(200, 100, 50, 128));
        string? key = null;
        foreach (var format in new[] { ImageFormat.Png, ImageFormat.WebP, ImageFormat.Png })
        {
            var encoded = ImageCodec.Encode(image, format);
            using var source = new MemoryStream(encoded);
            using var artifact = ContentPackageCreationManager.CreateImage(
                type,
                new ContentCreationIdentity("Changing encoding", "Summary", "Description", "1.0.0"),
                source
            );
            using var package = artifact.OpenRead();
            var installed = ContentInstallationManager.Install(package, new ContentInstallOptions(key));
            if (key is null)
            {
                key = installed.AssetKey!;
                _installedAssets.Add(
                    (type == ContentPackageType.BlocksTexture ? ContentType.BlocksTexture : ContentType.CharacterSkin,
                        key)
                );
            }

            Assert.Equal(key, installed.AssetKey);
            var directory = type == ContentPackageType.BlocksTexture
                ? GamePaths.BlockTextures
                : GamePaths.CharacterSkins;
            var extension = format == ImageFormat.Png ? ".png" : ".webp";
            var path = Storage.CombinePaths(directory, key + extension);
            using var input = Storage.OpenFile(path, OpenFileMode.Read);
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            Assert.Equal(encoded, bytes.ToArray());
            Assert.False(
                Storage.FileExists(
                    Storage.CombinePaths(directory, key + (format == ImageFormat.Png ? ".webp" : ".png"))
                )
            );
        }
    }

    [Theory]
    [InlineData(ContentPackageType.BlocksTexture, ImageFormat.Png)]
    [InlineData(ContentPackageType.BlocksTexture, ImageFormat.WebP)]
    [InlineData(ContentPackageType.CharacterSkin, ImageFormat.Png)]
    [InlineData(ContentPackageType.CharacterSkin, ImageFormat.WebP)]
    public void ImageCreationAndInstallationAcceptBothEncodings(ContentPackageType type, ImageFormat format)
    {
        using var image = new ImageBuffer(16, 16);
        var expected = new Color(200, 100, 50, 128);
        Array.Fill(image.Pixels, expected);
        var encoded = ImageCodec.Encode(image, format);
        using var source = new MemoryStream(encoded);
        using var artifact = ContentPackageCreationManager.CreateImage(
            type,
            new ContentCreationIdentity("Image encodings", "Summary", "Description", "1.0.0"),
            source
        );
        using var package = artifact.OpenRead();
        var inspection = ContentPackageReader.Inspect(package);
        Assert.Equal(format == ImageFormat.WebP ? "image/webp" : "image/png", inspection.Manifest.Payload.MediaType);
        var installed = ContentInstallationManager.Install(package);
        _installedAssets.Add(
            (type == ContentPackageType.BlocksTexture ? ContentType.BlocksTexture : ContentType.CharacterSkin,
                installed.AssetKey!)
        );
        string path;
        if (type == ContentPackageType.BlocksTexture)
        {
            path = BlocksTexturesManager.GetFileName(installed.AssetKey!);
        }
        else
        {
            Assert.True(CharacterSkinsManager.CreateStore().TryGetFileName(installed.AssetKey!, out path));
        }

        Assert.EndsWith(format == ImageFormat.WebP ? ".webp" : ".png", path, StringComparison.Ordinal);
        using var input = Storage.OpenFile(path, OpenFileMode.Read);
        using var installedBytes = new MemoryStream();
        input.CopyTo(installedBytes);
        Assert.Equal(encoded, installedBytes.ToArray());
        installedBytes.Position = 0;
        using var decoded = Image.Load(installedBytes, format);
        Assert.Equal(expected, decoded.Pixels[0]);
    }

    [Fact]
    public void ImageCreationProducesTemporaryValidatedPackageAndBaselineVersionKeepsIdentifier()
    {
        using var source = new MemoryStream(OnePixelPng(), writable: false);
        using var first = ContentPackageCreationManager.CreateImage(
            ContentPackageType.BlocksTexture,
            new ContentCreationIdentity("Created Texture", "Summary", "Description", "1.0.0"),
            source
        );
        using var firstPackage = first.OpenRead();
        var firstInspection = ContentPackageReader.Inspect(firstPackage);
        firstPackage.Position = 0;
        using var nextSource = new MemoryStream(OnePixelPng(), writable: false);
        using var next = ContentPackageCreationManager.CreateImage(
            ContentPackageType.BlocksTexture,
            new ContentCreationIdentity("Created Texture v2", "Summary", "Description", "2.0.0", firstPackage),
            nextSource
        );
        using var nextPackage = next.OpenRead();
        var nextInspection = ContentPackageReader.Inspect(nextPackage);

        Assert.Equal(ContentPackageType.BlocksTexture, firstInspection.Manifest.Type);
        Assert.Equal(firstInspection.Manifest.Identifier, nextInspection.Manifest.Identifier);
        Assert.Equal("2.0.0", nextInspection.Manifest.Version);
        Assert.NotEqual(first.PackageHash, next.PackageHash);
    }

    [Fact]
    public void ImageCreationRejectsUnsupportedFormatAndCleansTemporaryInput()
    {
        if (!Storage.DirectoryExists(GamePaths.ContentPackageCreationTemp))
        {
            Storage.CreateDirectory(GamePaths.ContentPackageCreationTemp);
        }

        var before = Storage.ListFileNames(GamePaths.ContentPackageCreationTemp)
            .ToHashSet(StringComparer.Ordinal);
        using var gif = new MemoryStream("GIF89a"u8.ToArray(), writable: false);

        Assert.ThrowsAny<Exception>(() => ContentPackageCreationManager.CreateImage(
                ContentPackageType.CharacterSkin,
                new ContentCreationIdentity("Not PNG", "Summary", "Description", "1.0.0"),
                gif
            )
        );

        Assert.Equal(
            before,
            Storage.ListFileNames(GamePaths.ContentPackageCreationTemp)
                .ToHashSet(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void WorldManufactureAndInstallPreserveBlockMappingAndExtensionData()
    {
        Storage.CreateDirectory(GamePaths.Worlds);
        var sourceDirectory = Storage.CombinePaths(GamePaths.Worlds, $"MappedSource{Guid.NewGuid():N}");
        Storage.CreateDirectory(sourceDirectory);
        _installedWorlds.Add(sourceDirectory);
        var project = Encoding.UTF8.GetBytes(
            "<Project Version=\"SCNET-1\" Guid=\"9e9a67f8-79df-4d05-8cfa-61bd8095661e\" Name=\"GameProject\">" +
            "<Subsystems /><Entities /><BlockRegistry><Block Id=\"example.mod:stone\" Index=\"300\" /></BlockRegistry>" +
            "<ExtensionData><Values Name=\"example.mod\"><Values Name=\"state\">" +
            "<Value Name=\"marker\" Type=\"string\" Value=\"preserved\" />" +
            "</Values></Values></ExtensionData></Project>"
        );
        using (var output = Storage.OpenFile(Storage.CombinePaths(sourceDirectory, "Project.xml"), OpenFileMode.Create))
        {
            output.Write(project);
        }

        using var artifact = ContentPackageCreationManager.CreateWorld(
            new ContentCreationIdentity("Mapped World", "Summary", "Description", "1.0.0"),
            Storage.GetFileName(sourceDirectory)
        );
        using var package = artifact.OpenRead();
        var installed = ContentInstallationManager.Install(package);
        var installedDirectory = Storage.CombinePaths(GamePaths.Worlds, installed.AssetKey!);
        _installedWorlds.Add(installedDirectory);
        using var input = Storage.OpenFile(Storage.CombinePaths(installedDirectory, "Project.xml"), OpenFileMode.Read);
        using var restored = new MemoryStream();
        input.CopyTo(restored);
        Assert.Equal(project, restored.ToArray());
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
                "<Project Version=\"SCNET-1\" Guid=\"9e9a67f8-79df-4d05-8cfa-61bd8095661e\" Name=\"GameProject\"><Subsystems /><Entities /><BlockRegistry /></Project>"
            );
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
            new ContentCreationIdentity("Large World", "Summary", "Description", "1.0.0"),
            Storage.GetFileName(sourceDirectory)
        );
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
                Game.Application.ClientApplication.Worlds.DeleteWorld(installedWorld);
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
        var metadata = JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                ["projectFormat"] = "scnet-project-xml-v1",
                ["regionsDirectory"] = "payload/world/Regions"
            }
        );
        var manifest = new ContentPackageManifest(
            ContentPackageManifest.CurrentFormatVersion,
            ContentPackageType.World,
            "9e9a67f8-79df-4d05-8cfa-61bd8095661e",
            "Installed World",
            "Summary",
            "Description",
            "1.0.0",
            new ContentPackagePayload("scnet.world-v1", "payload/world/Project.xml", "application/xml"),
            metadata
        );
        var project = Encoding.UTF8.GetBytes(
            "<Project Version=\"SCNET-1\" Guid=\"9e9a67f8-79df-4d05-8cfa-61bd8095661e\" Name=\"GameProject\"><Subsystems /><Entities /><BlockRegistry /></Project>"
        );
        var region = Encoding.UTF8.GetBytes(regionMarker);
        var output = new MemoryStream();
        ContentPackageWriter.Write(
            output,
            manifest,
            [
                new ContentPackageWriteEntry(
                    "payload/world/Project.xml",
                    project.Length,
                    () => new MemoryStream(project, writable: false)
                ),
                new ContentPackageWriteEntry(
                    "payload/world/Regions/0,0.dat",
                    region.Length,
                    () => new MemoryStream(region, writable: false)
                )
            ]
        );
        output.Position = 0;
        return output;
    }

    private static MemoryStream CreateImagePackage(ContentPackageType type, string displayName = "Installed Image")
    {
        var bytes = OnePixelPng();
        var isTexture = type == ContentPackageType.BlocksTexture;
        var entry = isTexture ? "payload/texture.png" : "payload/skin.png";
        var manifest = new ContentPackageManifest(
            ContentPackageManifest.CurrentFormatVersion,
            type,
            Guid.NewGuid().ToString(),
            displayName,
            "Summary",
            "Description",
            "1.0.0",
            new ContentPackagePayload(
                isTexture ? "scnet.blocks-texture.png-v1" : "scnet.character-skin.png-v1",
                entry,
                "image/png"
            ),
            JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["width"] = 1, ["height"] = 1 })
        );
        var output = new MemoryStream();
        ContentPackageWriter.Write(
            output,
            manifest,
            [new ContentPackageWriteEntry(entry, bytes.Length, () => new MemoryStream(bytes, writable: false))]
        );
        output.Position = 0;
        return output;
    }

    private static byte[] OnePixelPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAAC0lEQVR4nGP4DwQACfsD/fteaysAAAAASUVORK5CYII="
    );

    private sealed class ReadTrackingStream(Stream inner) : Stream
    {
        public int MaximumReadSize { get; private set; }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position { get => inner.Position; set => inner.Position = value; }

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
