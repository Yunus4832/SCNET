using System.IO.Compression;

using Content.Packaging;

namespace Game.Managers;

public interface IContentInstaller
{
    ContentPackageType Type { get; }
    ContentInstallResult Install(ContentPackageManifest manifest, ZipArchive package, ContentInstallOptions options);
}

public sealed record ContentInstallResult(ContentPackageType Type, string? AssetKey, string DisplayName);

public sealed record ContentInstallOptions(string? ReplaceAssetKey = null);

public static class ContentInstallationManager
{
    private static readonly IReadOnlyDictionary<ContentPackageType, IContentInstaller> _installers =
        new IContentInstaller[]
        {
            new ModContentInstaller(), new WorldContentInstaller(), new BlocksTextureContentInstaller(),
            new CharacterSkinContentInstaller(), new FurniturePackContentInstaller()
        }.ToDictionary(installer => installer.Type);

    public static ContentInstallResult Install(Stream package, ContentInstallOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (!package.CanSeek)
        {
            throw new ArgumentException("A validated, seekable cache package stream is required.", nameof(package));
        }

        package.Position = 0;
        var inspection = ContentPackageReader.Inspect(package);
        package.Position = 0;
        using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        return _installers[inspection.Manifest.Type].Install(inspection.Manifest, archive,
            options ?? new ContentInstallOptions());
    }

    private static Stream OpenPayload(ContentPackageManifest manifest, ZipArchive package) =>
        package.GetEntry(manifest.Payload.Entry)?.Open()
        ?? throw new ContentPackageException($"Package payload '{manifest.Payload.Entry}' is missing.");

    private sealed class ModContentInstaller : IContentInstaller
    {
        public ContentPackageType Type => ContentPackageType.Mod;

        public ContentInstallResult Install(ContentPackageManifest manifest, ZipArchive package,
            ContentInstallOptions options)
        {
            if (options.ReplaceAssetKey is not null)
            {
                throw new InvalidOperationException("Mod packages cannot replace installed assets.");
            }

            return new(Type, null, manifest.Name);
        }
    }

    private sealed class WorldContentInstaller : IContentInstaller
    {
        public ContentPackageType Type => ContentPackageType.World;

        public ContentInstallResult Install(ContentPackageManifest manifest, ZipArchive package,
            ContentInstallOptions options)
        {
            var name = options.ReplaceAssetKey is null
                ? WorldsManager.ImportWorldPackage(package)
                : WorldsManager.ReplaceWorldPackage(options.ReplaceAssetKey, package);
            return new(Type, Storage.GetFileName(name), manifest.Name);
        }
    }

    private sealed class BlocksTextureContentInstaller : IContentInstaller
    {
        public ContentPackageType Type => ContentPackageType.BlocksTexture;

        public ContentInstallResult Install(ContentPackageManifest manifest, ZipArchive package,
            ContentInstallOptions options)
        {
            using var payload = OpenPayload(manifest, package);
            var name = options.ReplaceAssetKey is null
                ? BlocksTexturesManager.ImportBlocksTexture(manifest.Name, payload)
                : BlocksTexturesManager.ReplaceBlocksTexture(options.ReplaceAssetKey, manifest.Name, payload);
            return new(Type, name, manifest.Name);
        }
    }

    private sealed class CharacterSkinContentInstaller : IContentInstaller
    {
        public ContentPackageType Type => ContentPackageType.CharacterSkin;

        public ContentInstallResult Install(ContentPackageManifest manifest, ZipArchive package,
            ContentInstallOptions options)
        {
            using var payload = OpenPayload(manifest, package);
            var name = options.ReplaceAssetKey is null
                ? CharacterSkinsManager.ImportCharacterSkin(manifest.Name, payload)
                : CharacterSkinsManager.ReplaceCharacterSkin(options.ReplaceAssetKey, manifest.Name, payload);
            return new(Type, name, manifest.Name);
        }
    }

    private sealed class FurniturePackContentInstaller : IContentInstaller
    {
        public ContentPackageType Type => ContentPackageType.FurniturePack;

        public ContentInstallResult Install(ContentPackageManifest manifest, ZipArchive package,
            ContentInstallOptions options)
        {
            using var payload = OpenPayload(manifest, package);
            var name = options.ReplaceAssetKey is null
                ? FurniturePacksManager.ImportFurnitureDesigns(manifest.Name, payload)
                : FurniturePacksManager.ReplaceFurnitureDesigns(options.ReplaceAssetKey, manifest.Name, payload);
            return new(Type, name, manifest.Name);
        }
    }
}
