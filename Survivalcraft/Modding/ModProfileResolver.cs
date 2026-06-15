using System.Security.Cryptography;

using System.Net.Http.Json;

namespace Game.Modding;

public static class ModProfileResolver
{
    public static IReadOnlyList<ModPackageSource> ResolveRequiredPackages(
        ModProfile profile,
        string localRepositoryPath,
        Action<string>? log = null)
    {
        profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Directory.CreateDirectory(localRepositoryPath);
        var repository = new LocalModRepository(localRepositoryPath);
        var resolvedSources = new List<ModPackageSource>();
        using var client = CreateHttpClient(profile.RepositoryUrl);

        foreach (var requirement in profile.Packages)
        {
            log?.Invoke($"解析模组 {requirement.ModId}@{requirement.Version}");
            var localEntry = repository.Find(requirement);
            if (localEntry == null && client != null)
            {
                log?.Invoke($"本地缺失 {requirement.ModId}@{requirement.Version}，尝试远程下载");
                localEntry = DownloadPackage(client, profile.RepositoryUrl!, requirement, repository, log);
            }

            if (localEntry == null)
            {
                throw new InvalidOperationException(
                    $"Required mod '{requirement.ModId}' version '{requirement.Version}' is missing and could not be resolved.");
            }

            resolvedSources.Add(new ModPackageSource(localEntry.FileName, () => File.OpenRead(localEntry.Path)));
        }

        return resolvedSources;
    }

    public static ModProfile BuildProfileFromDirectory(
        string profileId,
        string localRepositoryPath,
        string? repositoryUrl = null)
    {
        var repository = new LocalModRepository(localRepositoryPath);
        var profile = new ModProfile
        {
            Id = string.IsNullOrWhiteSpace(profileId) ? "default" : profileId.Trim(),
            RepositoryUrl = string.IsNullOrWhiteSpace(repositoryUrl) ? null : repositoryUrl.Trim().TrimEnd('/'),
            Packages = repository.ListAll()
                .OrderBy(entry => entry.ModId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.Version, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new ModPackageRequirement
                {
                    ModId = entry.ModId,
                    Version = entry.Version,
                    PackageHash = entry.PackageHash
                })
                .ToList()
        };
        return profile;
    }

    private static LocalModPackageEntry? DownloadPackage(
        HttpClient client,
        string repositoryUrl,
        ModPackageRequirement requirement,
        LocalModRepository repository,
        Action<string>? log)
    {
        var metadataUri = $"{repositoryUrl}/api/v1/mods/{Uri.EscapeDataString(requirement.ModId)}/versions/{Uri.EscapeDataString(requirement.Version)}";
        ModServerPackageMetadata? metadata;
        try
        {
            metadata = client.GetFromJsonAsync<ModServerPackageMetadata>(metadataUri).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to query remote repository for '{requirement.ModId}' version '{requirement.Version}': {ex.Message}",
                ex);
        }

        if (metadata == null || string.IsNullOrWhiteSpace(metadata.DownloadUrl))
        {
            return null;
        }

        var targetFileName = !string.IsNullOrWhiteSpace(metadata.FileName)
            ? metadata.FileName
            : $"{requirement.ModId}.{requirement.Version}.scpak";
        var targetPath = repository.GetTargetPath(targetFileName);
        try
        {
            var content = client.GetByteArrayAsync(metadata.DownloadUrl).GetAwaiter().GetResult();
            File.WriteAllBytes(targetPath, content);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to download '{requirement.ModId}' version '{requirement.Version}': {ex.Message}",
                ex);
        }

        log?.Invoke($"已下载模组 {requirement.ModId}@{requirement.Version}");
        repository.Invalidate();
        return repository.Find(requirement);
    }

    private static HttpClient? CreateHttpClient(string? repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return null;
        }

        return new HttpClient
        {
            BaseAddress = new Uri(repositoryUrl.TrimEnd('/') + "/")
        };
    }

    private sealed class LocalModRepository
    {
        private readonly string _directoryPath;
        private List<LocalModPackageEntry>? _entries;

        public LocalModRepository(string directoryPath)
        {
            _directoryPath = directoryPath;
        }

        public IReadOnlyList<LocalModPackageEntry> ListAll()
        {
            EnsureIndexed();
            return _entries!;
        }

        public LocalModPackageEntry? Find(ModPackageRequirement requirement)
        {
            EnsureIndexed();
            return _entries!.FirstOrDefault(entry =>
                string.Equals(entry.ModId, requirement.ModId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(entry.Version, requirement.Version, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(requirement.PackageHash) ||
                 string.Equals(entry.PackageHash, requirement.PackageHash, StringComparison.OrdinalIgnoreCase)));
        }

        public string GetTargetPath(string fileName)
        {
            var sanitized = Path.GetFileName(fileName);
            return Path.Combine(_directoryPath, sanitized);
        }

        public void Invalidate()
        {
            _entries = null;
        }

        private void EnsureIndexed()
        {
            if (_entries != null)
            {
                return;
            }

            Directory.CreateDirectory(_directoryPath);
            var sources = Directory.EnumerateFiles(_directoryPath, ModPackage.SearchPattern, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => new ModPackageSource(Path.GetFileName(path), () => File.OpenRead(path)))
                .ToArray();
            var packages = ModPackageCatalog.Discover(sources);
            _entries = packages.Zip(sources, (package, source) => new LocalModPackageEntry(
                    Path.Combine(_directoryPath, source.Name),
                    source.Name,
                    package.Manifest.Id,
                    package.Manifest.Version,
                    ComputeHash(Path.Combine(_directoryPath, source.Name))))
                .ToList();
        }

        private static string ComputeHash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }
    }

    private sealed record LocalModPackageEntry(
        string Path,
        string FileName,
        string ModId,
        string Version,
        string PackageHash);

    private sealed class ModServerPackageMetadata
    {
        public string ModId { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public string PackageHash { get; set; } = string.Empty;

        public string FileName { get; set; } = string.Empty;

        public string DownloadUrl { get; set; } = string.Empty;
    }
}
