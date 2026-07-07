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
        using var client = CreateClient(GetRepositoryUrl(profile));

        foreach (var requirement in profile.Packages)
        {
            log?.Invoke($"解析模组 {requirement.ModId}@{requirement.Version}");
            var localEntry = repository.Find(requirement);
            if (localEntry == null && client != null)
            {
                log?.Invoke($"本地缺失 {requirement.ModId}@{requirement.Version}，尝试远程下载");
                localEntry = DownloadPackage(client, requirement, repository, log);
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

    public static bool EnsurePackagesAvailable(
        ModProfile profile,
        string localRepositoryPath,
        Action<string>? log = null)
    {
        profile = profile ?? throw new ArgumentNullException(nameof(profile));
        Directory.CreateDirectory(localRepositoryPath);
        var repository = new LocalModRepository(localRepositoryPath);
        using var client = CreateClient(GetRepositoryUrl(profile));
        var downloadedAny = false;

        foreach (var requirement in profile.Packages)
        {
            log?.Invoke($"检查模组 {requirement.ModId}@{requirement.Version}");
            var localEntry = repository.Find(requirement);
            if (localEntry == null && client != null)
            {
                log?.Invoke($"本地缺失 {requirement.ModId}@{requirement.Version}，尝试远程下载");
                localEntry = DownloadPackage(client, requirement, repository, log);
                downloadedAny |= localEntry != null;
            }

            if (localEntry == null)
            {
                throw new InvalidOperationException(
                    $"Required mod '{requirement.ModId}' version '{requirement.Version}' is missing and could not be resolved.");
            }
        }

        return downloadedAny;
    }

    private static LocalModPackageEntry? DownloadPackage(
        ModServerClient client,
        ModPackageRequirement requirement,
        LocalModRepository repository,
        Action<string>? log)
    {
        ModRepositoryPackage? metadata;
        try
        {
            metadata = client.FindPackage(requirement.ModId, requirement.Version);
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

        try
        {
            var localEntry = client.DownloadPackage(metadata, repository);
            log?.Invoke($"已下载模组 {requirement.ModId}@{requirement.Version}");
            return localEntry;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to download '{requirement.ModId}' version '{requirement.Version}': {ex.Message}",
                ex);
        }
    }

    private static ModServerClient? CreateClient(string? repositoryUrl)
    {
        return string.IsNullOrWhiteSpace(repositoryUrl) ? null : new ModServerClient(repositoryUrl);
    }

    private static string? GetRepositoryUrl(ModProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.RepositoryUrl)
            ? SettingsManager.Current.DefaultModRepositoryUrl
            : profile.RepositoryUrl;
    }
}
