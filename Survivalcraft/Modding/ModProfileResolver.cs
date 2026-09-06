using Game.Content;

namespace Game.Modding;

public static class ModProfileResolver
{
    public static IReadOnlyList<ModPackageSource> ResolveRequiredPackages(
        ModProfile profile,
        string localRepositoryPath,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ModProfileValidation.Validate(profile);
        Directory.CreateDirectory(localRepositoryPath);
        var repository = new LocalModRepository(localRepositoryPath);
        var download = CreateDownloadService(localRepositoryPath);
        var context = ContentSourceContext.Persistent(SettingsManager.Current.ContentRepositories);
        var resolvedSources = new List<ModPackageSource>();
        foreach (var requirement in profile.Packages)
        {
            var localEntry = ResolveEntry(requirement, repository, download, context, log, out _);
            resolvedSources.Add(new ModPackageSource(localEntry.FileName, () => File.OpenRead(localEntry.Path)));
        }

        return resolvedSources;
    }

    public static bool EnsurePackagesAvailable(
        ModProfile profile,
        string localRepositoryPath,
        Action<string>? log = null)
    {
        return EnsurePackagesAvailable(profile, localRepositoryPath,
            ContentSourceContext.Persistent(SettingsManager.Current.ContentRepositories), log);
    }

    public static bool EnsurePackagesAvailable(
        ModProfile profile,
        string localRepositoryPath,
        ContentSourceContext context,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);
        ModProfileValidation.Validate(profile);
        Directory.CreateDirectory(localRepositoryPath);
        var repository = new LocalModRepository(localRepositoryPath);
        var download = CreateDownloadService(localRepositoryPath);
        var downloadedAny = false;
        foreach (var requirement in profile.Packages)
        {
            _ = ResolveEntry(requirement, repository, download, context, log, out var downloaded);
            downloadedAny |= downloaded;
        }

        return downloadedAny;
    }

    private static LocalModPackageEntry ResolveEntry(ModPackageRequirement requirement,
        LocalModRepository repository, ContentDownloadService download, ContentSourceContext context,
        Action<string>? log, out bool downloaded)
    {
        log?.Invoke($"检查模组 {requirement.ModId}@{requirement.Version} ({requirement.PackageHash})");
        var localEntry = repository.Find(requirement);
        if (localEntry is not null)
        {
            log?.Invoke($"本地缓存命中 {requirement.ModId}@{requirement.Version}");
            downloaded = false;
            return localEntry;
        }

        log?.Invoke($"本地缺失 {requirement.ModId}@{requirement.Version}，查询候选内容仓库");
        try
        {
            var result = download.DownloadExactModAsync(context, requirement.ModId, requirement.Version,
                    requirement.PackageHash)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            foreach (var failure in result.PriorFailures)
            {
                log?.Invoke($"仓库 {failure.RepositoryName} 失败: {failure.Message}");
            }

            repository.Invalidate();
            localEntry = repository.Find(requirement)
                         ?? throw new InvalidDataException("Downloaded package was not found in the local cache.");
            log?.Invoke($"已下载模组 {requirement.ModId}@{requirement.Version}");
            downloaded = !result.WasCached;
            return localEntry;
        }
        catch (ContentDownloadException exception)
        {
            foreach (var failure in exception.Failures)
            {
                log?.Invoke($"仓库 {failure.RepositoryName} 失败: {failure.Message}");
            }

            throw new InvalidOperationException(
                $"Required mod '{requirement.ModId}' version '{requirement.Version}' with hash " +
                $"'{requirement.PackageHash}' is missing from all candidate repositories.", exception);
        }
    }

    private static ContentDownloadService CreateDownloadService(string localRepositoryPath)
    {
        return new ContentDownloadService(SettingsManager.ContentClients,
            new ContentPackageCache(localRepositoryPath));
    }
}
