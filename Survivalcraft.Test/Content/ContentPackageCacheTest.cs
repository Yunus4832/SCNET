using Content.Packaging;

using Game.Content;

using Survivalcraft.Test.Modding;

namespace Survivalcraft.Test.Content;

public sealed class ContentPackageCacheTest : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"scnet-package-cache-{Guid.NewGuid():N}");

    [Fact]
    public async Task ImportUsesLogicalHashAddressAndPreservesFirstArtifact()
    {
        var bytes = CreatePackage();
        var cache = new ContentPackageCache(_directory);
        await using var first = new MemoryStream(bytes, writable: false);
        var firstEntry = await cache.ImportAsync(first);
        var firstWrite = File.GetLastWriteTimeUtc(firstEntry.Path);

        await using var repeated = new MemoryStream(bytes, writable: false);
        var repeatedEntry = await cache.ImportAsync(repeated);

        Assert.Equal(firstEntry.PackageHash, repeatedEntry.PackageHash);
        Assert.Equal($"{firstEntry.PackageHash}.scpkg", Path.GetFileName(firstEntry.Path));
        Assert.Equal(firstWrite, File.GetLastWriteTimeUtc(repeatedEntry.Path));
        Assert.Single(cache.List());
    }

    [Fact]
    public async Task CorruptAddressedFileIsExcludedAndCannotBeOpened()
    {
        var cache = new ContentPackageCache(_directory);
        await using var source = new MemoryStream(CreatePackage(), writable: false);
        var entry = await cache.ImportAsync(source);
        await File.WriteAllBytesAsync(entry.Path, [1, 2, 3, 4]);

        cache.RebuildIndex();

        Assert.Empty(cache.List());
        Assert.Throws<ContentPackageException>(() => cache.OpenValidated(entry.PackageHash));
    }

    [Fact]
    public async Task CancelledImportRemovesTemporaryFile()
    {
        var cache = new ContentPackageCache(_directory);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await using var source = new MemoryStream(CreatePackage(), writable: false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.ImportAsync(source, cancellation.Token));

        var temporaryDirectory = Path.Combine(_directory, ".temp");
        Assert.Empty(Directory.EnumerateFiles(temporaryDirectory));
    }

    [Fact]
    public async Task ExportCopiesTheValidatedOriginalArtifact()
    {
        var bytes = CreatePackage();
        var cache = new ContentPackageCache(_directory);
        await using var source = new MemoryStream(bytes, writable: false);
        var entry = await cache.ImportAsync(source);
        await using var destination = new MemoryStream();

        await cache.ExportAsync(entry.PackageHash, destination);

        Assert.Equal(bytes, destination.ToArray());
    }

    [Fact]
    public async Task ExpectedTypeMismatchDoesNotCommitPackage()
    {
        var cache = new ContentPackageCache(_directory);
        await using var source = new MemoryStream(CreatePackage(), writable: false);

        await Assert.ThrowsAsync<ContentPackageException>(() =>
            cache.ImportExpectedAsync(source, ContentPackageType.World));

        Assert.Empty(cache.List());
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_directory, ".temp")));
    }

    [Fact]
    public async Task AllowedTypesRejectModBeforeCommittingPackage()
    {
        var cache = new ContentPackageCache(_directory);
        await using var source = new MemoryStream(CreatePackage(), writable: false);

        await Assert.ThrowsAsync<ContentPackageException>(() => cache.ImportAllowedAsync(source,
            [ContentPackageType.World, ContentPackageType.BlocksTexture, ContentPackageType.CharacterSkin,
                ContentPackageType.FurniturePack]));

        Assert.Empty(cache.List());
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(_directory, ".temp")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private static byte[] CreatePackage()
    {
        using var stream = ScpkgTestPackage.Create("""
            {"id":"cache.example","name":"Cache Example","version":"1.0.0"}
            """, new Dictionary<string, string> { ["data/marker.txt"] = "cache" });
        return stream.ToArray();
    }
}
