using System.Globalization;

using Game.Localization;

namespace Game.Modding;

public static class ModProfileResolver
{
    public static IReadOnlyList<ModPackageSource> ResolveRequiredPackages(
        ModProfile profile,
        string localRepositoryPath,
        Action<string>? log = null
    )
    {
        ArgumentNullException.ThrowIfNull(profile);
        ModProfileValidation.Validate(profile);
        Directory.CreateDirectory(localRepositoryPath);
        var repository = new LocalModRepository(localRepositoryPath);
        var download = CreateDownloadService(localRepositoryPath);
        var context = ContentSourceContext.Persistent(ContentRepositoryManager.Current.Snapshot());
        var resolvedSources = new List<ModPackageSource>();
        foreach (var requirement in profile.Packages)
        {
            var localEntry = ResolveEntry(
                requirement,
                repository,
                download,
                context,
                log,
                CancellationToken.None,
                out _
            );
            resolvedSources.Add(new ModPackageSource(localEntry.FileName, () => File.OpenRead(localEntry.Path)));
        }

        return resolvedSources;
    }

    public static bool EnsurePackagesAvailable(
        ModProfile profile,
        string localRepositoryPath,
        Action<string>? log = null,
        LocalizedTextResolver? textResolver = null
    )
    {
        return EnsurePackagesAvailable(
            profile,
            localRepositoryPath,
            ContentSourceContext.Persistent(ContentRepositoryManager.Current.Snapshot()),
            log,
            textResolver: textResolver
        );
    }

    public static bool EnsurePackagesAvailable(
        ModProfile profile,
        string localRepositoryPath,
        ContentSourceContext context,
        Action<string>? log = null,
        CancellationToken cancellationToken = default,
        LocalizedTextResolver? textResolver = null
    )
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        ModProfileValidation.Validate(profile);
        Directory.CreateDirectory(localRepositoryPath);
        var repository = new LocalModRepository(localRepositoryPath);
        var download = CreateDownloadService(localRepositoryPath);
        var downloadedAny = false;
        foreach (var requirement in profile.Packages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = ResolveEntry(
                requirement,
                repository,
                download,
                context,
                log,
                cancellationToken,
                out var downloaded,
                textResolver
            );
            downloadedAny |= downloaded;
        }

        return downloadedAny;
    }

    private static LocalModPackageEntry ResolveEntry(
        ModPackageRequirement requirement,
        LocalModRepository repository,
        ContentDownloadService download,
        ContentSourceContext context,
        Action<string>? log,
        CancellationToken cancellationToken,
        out bool downloaded,
        LocalizedTextResolver? textResolver = null
    )
    {
        WriteProgress(
            log,
            textResolver,
            "Checking",
            "检查模组 {0}@{1} ({2})",
            requirement.ModId,
            requirement.Version,
            requirement.PackageHash
        );
        var localEntry = repository.Find(requirement);
        if (localEntry is not null)
        {
            WriteProgress(log, textResolver, "Cached", "本地缓存命中 {0}@{1}", requirement.ModId, requirement.Version);
            downloaded = false;
            return localEntry;
        }

        WriteProgress(
            log,
            textResolver,
            "Downloading",
            "本地缺失 {0}@{1}，查询候选内容仓库",
            requirement.ModId,
            requirement.Version
        );
        try
        {
            var result = download.DownloadExactModAsync(
                    context,
                    requirement.ModId,
                    requirement.Version,
                    requirement.PackageHash,
                    cancellationToken
                )
                .ConfigureAwait(false).GetAwaiter().GetResult();
            foreach (var failure in result.PriorFailures)
            {
                WriteProgress(
                    log,
                    textResolver,
                    "RepositoryFailed",
                    "仓库 {0} 失败: {1}",
                    failure.RepositoryName,
                    failure.Message
                );
            }

            repository.Invalidate();
            localEntry = repository.Find(requirement) ??
                         throw new InvalidDataException("Downloaded package was not found in the local cache.");
            WriteProgress(log, textResolver, "Downloaded", "已下载模组 {0}@{1}", requirement.ModId, requirement.Version);
            downloaded = !result.WasCached;
            return localEntry;
        }
        catch (ContentDownloadException exception)
        {
            foreach (var failure in exception.Failures)
            {
                WriteProgress(
                    log,
                    textResolver,
                    "RepositoryFailed",
                    "仓库 {0} 失败: {1}",
                    failure.RepositoryName,
                    failure.Message
                );
            }

            throw new InvalidOperationException(
                $"Required mod '{requirement.ModId}' version '{requirement.Version}' with hash " +
                $"'{requirement.PackageHash}' is missing from all candidate repositories.",
                exception
            );
        }
    }

    private static void WriteProgress(
        Action<string>? log,
        LocalizedTextResolver? resolver,
        string key,
        string fallback,
        params string[] arguments
    )
    {
        log?.Invoke(
            resolver is null
                ? string.Format(CultureInfo.InvariantCulture, fallback, arguments.Cast<object>().ToArray())
                : resolver.Get("ModPreparation", key, fallback, arguments)
        );
    }

    private static ContentDownloadService CreateDownloadService(string localRepositoryPath)
    {
        return new ContentDownloadService(
            ContentServerClientPool.Shared,
            new ContentPackageCache(localRepositoryPath)
        );
    }
}
