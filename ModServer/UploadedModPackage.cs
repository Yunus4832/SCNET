using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ModServer;

public sealed class UploadedModPackage
{
    private const int _maxManifestSize = 1024 * 1024;
    private const long _maxAssemblyBytes = 128L * 1024 * 1024;
    private const long _maxDataBytes = 128L * 1024 * 1024;
    private const long _maxAssetBytes = 256L * 1024 * 1024;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private UploadedModPackage(
        string modId,
        string version,
        string side,
        string packageHash,
        string fileName,
        long packageSize)
    {
        ModId = modId;
        Version = version;
        Side = side;
        PackageHash = packageHash;
        FileName = fileName;
        PackageSize = packageSize;
    }

    public string ModId { get; }

    public string Version { get; }

    public string Side { get; }

    public string PackageHash { get; }

    public string FileName { get; }

    public long PackageSize { get; }

    public static UploadedModPackage Read(string fileName, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        using var stream = new MemoryStream(content, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        var manifestEntry = archive.GetEntry("manifest.json")
                            ?? throw new InvalidOperationException("Package does not contain manifest.json.");
        if (manifestEntry.Length > _maxManifestSize)
        {
            throw new InvalidOperationException("Manifest is too large.");
        }

        string manifestJson;
        using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8, true, leaveOpen: false))
        {
            manifestJson = reader.ReadToEnd();
        }

        var manifest = ParseManifest(manifestJson);
        long totalAssemblyBytes = 0;
        long totalDataBytes = 0;
        long totalAssetBytes = 0;
        var assemblies = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var dataFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var assetFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            if (entry.FullName.StartsWith("data/", StringComparison.Ordinal))
            {
                totalDataBytes += entry.Length;
                if (totalDataBytes > _maxDataBytes)
                {
                    throw new InvalidOperationException("Package data files exceed the size limit.");
                }

                dataFiles.Add(entry.FullName, ReadEntryBytes(entry));
                continue;
            }

            var assetPrefix = $"assets/{manifest.ModId}/";
            if (entry.FullName.StartsWith("assets/", StringComparison.Ordinal))
            {
                if (!entry.FullName.StartsWith(assetPrefix, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Asset {entry.FullName} must be inside {assetPrefix}.");
                }

                totalAssetBytes += entry.Length;
                if (totalAssetBytes > _maxAssetBytes)
                {
                    throw new InvalidOperationException("Package assets exceed the size limit.");
                }

                var relativePath = entry.FullName[assetPrefix.Length..];
                ValidateAssetPath(relativePath);
                assetFiles.Add(relativePath, ReadEntryBytes(entry));
                continue;
            }

            if (!entry.FullName.StartsWith("assemblies/", StringComparison.Ordinal) ||
                !entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            totalAssemblyBytes += entry.Length;
            if (totalAssemblyBytes > _maxAssemblyBytes)
            {
                throw new InvalidOperationException("Package assemblies exceed the size limit.");
            }

            var assemblyName = Path.GetFileNameWithoutExtension(entry.Name);
            assemblies.Add(assemblyName, ReadEntryBytes(entry));
        }

        return new UploadedModPackage(
            manifest.ModId,
            manifest.Version,
            manifest.NormalizedSide,
            ComputePackageHash(manifestJson, assemblies, dataFiles, assetFiles),
            Path.GetFileName(fileName),
            content.LongLength);
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var source = entry.Open();
        using var memory = new MemoryStream((int)entry.Length);
        source.CopyTo(memory);
        return memory.ToArray();
    }

    private static UploadedManifest ParseManifest(string manifestJson)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<UploadedManifest>(manifestJson, _jsonOptions)
                           ?? throw new InvalidOperationException("Mod manifest is empty.");
            manifest.Validate();
            return manifest;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Manifest is invalid.", exception);
        }
    }

    private static string ComputePackageHash(
        string manifestJson,
        IReadOnlyDictionary<string, byte[]> assemblies,
        IReadOnlyDictionary<string, byte[]> dataFiles,
        IReadOnlyDictionary<string, byte[]> assetFiles)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append("manifest.json", Encoding.UTF8.GetBytes(manifestJson));
        foreach (var (name, bytes) in assemblies.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
        {
            Append($"assemblies/{name}.dll", bytes);
        }

        foreach (var (path, bytes) in dataFiles.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            Append(path, bytes);
        }

        foreach (var (path, bytes) in assetFiles.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            Append($"assets/{path}", bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());

        void Append(string path, byte[] bytes)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(path));
            hash.AppendData([0]);
            hash.AppendData(bytes);
        }
    }

    private static void ValidateAssetPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.StartsWith('/') ||
            relativePath.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new InvalidOperationException($"Asset path {relativePath} is invalid.");
        }
    }

    private sealed class UploadedManifest
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string? Side { get; set; }

        public string ModId => Id.Trim();

        public string NormalizedSide => NormalizeSide(Side);

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ModId))
            {
                throw new InvalidOperationException("Mod id is required.");
            }

            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException("Mod name is required.");
            }

            Version = Version.Trim();
            if (!System.Version.TryParse(Version, out _))
            {
                throw new InvalidOperationException($"Mod {ModId} has invalid version \"{Version}\".");
            }

            _ = NormalizedSide;
        }

        private static string NormalizeSide(string? side)
        {
            return side?.Trim().ToLowerInvariant() switch
            {
                null or "" => "common",
                "common" => "common",
                "client" => "client",
                "server" => "server",
                _ => throw new InvalidOperationException($"Unsupported mod side \"{side}\".")
            };
        }
    }
}
