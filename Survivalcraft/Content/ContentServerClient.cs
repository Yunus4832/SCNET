using System.Net.Http.Json;

namespace Game.Content;

public sealed class ContentServerClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;

    public ContentServerClient(string serverUrl, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);
        _disposeClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri($"{serverUrl.Trim().TrimEnd('/')}/");
    }

    public async Task<IReadOnlyList<ContentCatalogItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<ContentCatalogItem>();
        for (var pageIndex = 1;; pageIndex++)
        {
            var response = await _httpClient
                .GetFromJsonAsync<ContentServerResponse<ContentServerPage<ContentCatalogItem>>>(
                    $"api/v1/content?pageIndex={pageIndex}&pageSize=10",
                    cancellationToken)
                .ConfigureAwait(false);
            var page = response?.Data;
            if (page is null || page.Items.Count == 0)
            {
                break;
            }

            items.AddRange(page.Items);
            if (items.Count >= page.Total)
            {
                break;
            }
        }

        return items;
    }

    public IReadOnlyList<ContentServerModPackage> ListMods() => RunSync(ListModsAsync);

    public ContentServerModPackage? FindMod(string modId, string version) =>
        RunSync(cancellationToken => FindModAsync(modId, version, cancellationToken));

    public LocalModPackageEntry DownloadMod(ContentServerModPackage package, LocalModRepository repository) =>
        RunSync(cancellationToken => DownloadModAsync(package, repository, cancellationToken));

    private async Task<IReadOnlyList<ContentServerModPackage>> ListModsAsync(CancellationToken cancellationToken)
    {
        var items = new List<ContentServerModPackage>();
        for (var pageIndex = 1;; pageIndex++)
        {
            var response = await _httpClient
                .GetFromJsonAsync<ContentServerResponse<ContentServerPage<ContentServerModPackage>>>(
                    $"api/v1/mods?pageIndex={pageIndex}&pageSize=10", cancellationToken)
                .ConfigureAwait(false);
            var page = response?.Data;
            if (page is null || page.Items.Count == 0)
            {
                break;
            }

            items.AddRange(page.Items);
            if (items.Count >= page.Total)
            {
                break;
            }
        }

        return items;
    }

    private async Task<ContentServerModPackage?> FindModAsync(string modId, string version,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        using var response = await _httpClient.GetAsync(
            $"api/v1/mods/{Uri.EscapeDataString(modId)}/versions/{Uri.EscapeDataString(version)}",
            cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content
            .ReadFromJsonAsync<ContentServerResponse<ContentServerModPackage>>(
                cancellationToken: cancellationToken).ConfigureAwait(false);
        return result?.Data;
    }

    private async Task<LocalModPackageEntry> DownloadModAsync(ContentServerModPackage package,
        LocalModRepository repository, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(repository);
        var existing = repository.FindByHash(package.PackageHash);
        if (existing is not null)
        {
            return existing;
        }

        using var response = await _httpClient.GetAsync(package.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var entry = repository.AddPackage(content);
        if (!string.Equals(entry.PackageHash, package.PackageHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Downloaded package hash does not match ContentServer metadata.");
        }

        return entry;
    }

    public async Task<ContentPackageCacheEntry> DownloadToCacheAsync(
        ContentCatalogItem item,
        IContentPackageCache cache,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(cache);
        var existing = cache.Find(item.PackageHash);
        if (existing is not null)
        {
            return existing;
        }

        using var response = await _httpClient.GetAsync(item.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var imported = await cache.ImportAsync(stream, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(imported.PackageHash, item.PackageHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Downloaded package hash does not match ContentServer metadata.");
        }

        return imported;
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }

    private static T RunSync<T>(Func<CancellationToken, Task<T>> action) =>
        action(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
}

public sealed class ContentCatalogItem
{
    public string ContentId { get; init; } = string.Empty;
    public string PublisherId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Identifier { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string VersionId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string PackageHash { get; init; } = string.Empty;
    public long PackageSize { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string DownloadUrl { get; init; } = string.Empty;
}

public sealed class ContentServerModPackage
{
    public string ModId { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string PackageHash { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public long PackageSize { get; init; }
    public string Side { get; init; } = "common";
    public string? Description { get; init; }
    public DateTimeOffset UploadedAtUtc { get; init; }
    public string DownloadUrl { get; init; } = string.Empty;
}
