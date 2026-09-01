using System.Text.Json;
using System.Xml.Linq;

using Content.Packaging;

using Engine.Media;

namespace Game.Content;

public sealed record ContentCreationIdentity(string Name, string Version, Stream? BaselinePackage = null);

public sealed class ContentPackageCreationArtifact(string path, string packageHash) : IDisposable
{
    public string PackageHash { get; } = packageHash;
    public Stream OpenRead() => Storage.OpenFile(path, OpenFileMode.Read);
    public void Dispose()
    {
        if (Storage.FileExists(path)) Storage.DeleteFile(path);
    }
}

public static class ContentPackageCreationManager
{
    public static ContentPackageCreationArtifact CreateImage(ContentPackageType type,
        ContentCreationIdentity identity, Stream source)
    {
        if (type is not (ContentPackageType.BlocksTexture or ContentPackageType.CharacterSkin))
            throw new ArgumentOutOfRangeException(nameof(type));
        ArgumentNullException.ThrowIfNull(source);
        Storage.CreateDirectory(GamePaths.ContentPackageCreationTemp);
        var sourcePath = Storage.CombinePaths(GamePaths.ContentPackageCreationTemp, $"{Guid.NewGuid():N}.png.temp");
        try
        {
            using (var output = Storage.OpenFile(sourcePath, OpenFileMode.Create)) source.CopyTo(output);
            int width;
            int height;
            using (var input = Storage.OpenFile(sourcePath, OpenFileMode.Read))
            {
                var image = Image.Load(input, ImageFileFormat.Png);
                width = image.Width;
                height = image.Height;
            }
            var isTexture = type == ContentPackageType.BlocksTexture;
            var entry = isTexture ? "payload/texture.png" : "payload/skin.png";
            var manifest = CreateManifest(type, identity,
                new ContentPackagePayload(isTexture ? "scnet.blocks-texture.png-v1" : "scnet.character-skin.png-v1",
                    entry, "image/png"),
                JsonSerializer.SerializeToElement(new Dictionary<string, object>
                    { ["width"] = width, ["height"] = height }));
            return Write(manifest,
            [
                new ContentPackageWriteEntry(entry, Storage.GetFileSize(sourcePath),
                    () => Storage.OpenFile(sourcePath, OpenFileMode.Read))
            ]);
        }
        finally
        {
            if (Storage.FileExists(sourcePath)) Storage.DeleteFile(sourcePath);
        }
    }

    public static ContentPackageCreationArtifact CreateWorld(ContentCreationIdentity identity, string assetKey)
    {
        var directory = Storage.CombinePaths(GamePaths.Worlds, assetKey);
        var project = Storage.CombinePaths(directory, "Project.xml");
        if (!Storage.FileExists(project)) throw new InvalidOperationException($"World '{assetKey}' does not exist.");
        var entries = new List<ContentPackageWriteEntry> { Entry("payload/world/Project.xml", project) };
        var regions = Storage.CombinePaths(directory, "Regions");
        if (Storage.DirectoryExists(regions))
            entries.AddRange(Storage.ListFileNames(regions).OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => Entry($"payload/world/Regions/{name}", Storage.CombinePaths(regions, name))));
        var manifest = CreateManifest(ContentPackageType.World, identity,
            new ContentPackagePayload("scnet.world-v1", "payload/world/Project.xml", "application/xml"),
            JsonSerializer.SerializeToElement(new Dictionary<string, object>
            {
                ["projectFormat"] = "scnet-project-xml-v1",
                ["regionsDirectory"] = "payload/world/Regions"
            }));
        return Write(manifest, entries);
    }

    public static ContentPackageCreationArtifact CreateFurniture(ContentCreationIdentity identity, string assetKey)
    {
        var path = Managers.FurniturePacksManager.GetFileName(assetKey);
        if (!Storage.FileExists(path)) throw new InvalidOperationException($"Furniture asset '{assetKey}' does not exist.");
        int count;
        using (var input = Storage.OpenFile(path, OpenFileMode.Read))
            count = XDocument.Load(input).Root?.Elements().Count() ?? 0;
        var payload = "payload/furniture/FurnitureDesigns.xml";
        var manifest = CreateManifest(ContentPackageType.FurniturePack, identity,
            new ContentPackagePayload("scnet.furniture-designs-xml-v1", payload, "application/xml"),
            JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["designCount"] = count }));
        return Write(manifest, [Entry(payload, path)]);
    }

    private static ContentPackageManifest CreateManifest(ContentPackageType type, ContentCreationIdentity identity,
        ContentPackagePayload payload, JsonElement metadata)
    {
        _ = SemanticVersion.Parse(identity.Version);
        var identifier = Guid.NewGuid().ToString();
        if (identity.BaselinePackage is not null)
        {
            if (!identity.BaselinePackage.CanSeek)
                throw new ArgumentException("Baseline package must be seekable.", nameof(identity));
            identity.BaselinePackage.Position = 0;
            var baseline = ContentPackageReader.Inspect(identity.BaselinePackage);
            identity.BaselinePackage.Position = 0;
            if (baseline.Manifest.Type != type)
                throw new ContentPackageException("Baseline package type does not match the creation type.");
            identifier = baseline.Manifest.Identifier;
        }
        return new ContentPackageManifest(ContentPackageManifest.CurrentFormatVersion, type, identifier,
            identity.Name, identity.Version, payload, metadata);
    }

    private static ContentPackageCreationArtifact Write(ContentPackageManifest manifest,
        IReadOnlyList<ContentPackageWriteEntry> entries)
    {
        Storage.CreateDirectory(GamePaths.ContentPackageCreationTemp);
        var path = Storage.CombinePaths(GamePaths.ContentPackageCreationTemp, $"{Guid.NewGuid():N}.scpkg.temp");
        try
        {
            string hash;
            using (var output = Storage.OpenFile(path, OpenFileMode.Create))
                hash = ContentPackageWriter.Write(output, manifest, entries);
            return new ContentPackageCreationArtifact(path, hash);
        }
        catch
        {
            if (Storage.FileExists(path)) Storage.DeleteFile(path);
            throw;
        }
    }

    private static ContentPackageWriteEntry Entry(string packagePath, string storagePath) =>
        new(packagePath, Storage.GetFileSize(storagePath), () => Storage.OpenFile(storagePath, OpenFileMode.Read));
}
