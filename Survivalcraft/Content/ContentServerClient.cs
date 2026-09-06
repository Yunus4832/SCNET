using System.Text.Json;

using Content.Packaging;

namespace Game.Content;

public sealed class ContentServerClient : IDisposable
{
    public const int MaximumJsonResponseBytes = 4 * 1024 * 1024;

    private const int _maximumCatalogPages = 100;
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;

    public ContentServerClient(string serverUrl, HttpClient? httpClient = null)
        : this(serverUrl, httpClient ?? new HttpClient(), httpClient is null)
    {
    }

    internal ContentServerClient(string serverUrl, HttpClient httpClient, bool disposeClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverUrl);
        ArgumentNullException.ThrowIfNull(httpClient);
        _disposeClient = disposeClient;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri($"{serverUrl.Trim().TrimEnd('/')}/");
    }

    public async Task<ContentServerHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        using var httpResponse = await _httpClient.GetAsync("api/v1/health", HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();
        var response = await ReadJsonAsync<ContentServerResponse<ContentServerHealth>>(httpResponse,
            cancellationToken).ConfigureAwait(false);
        if (response?.Success != true || response.Data is null ||
            string.IsNullOrWhiteSpace(response.Data.Name) || string.IsNullOrWhiteSpace(response.Data.Version))
        {
            throw new InvalidDataException("ContentServer returned an invalid health response.");
        }

        return response.Data;
    }

    public async Task<IReadOnlyList<ContentCatalogItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<ContentCatalogItem>();
        for (var pageIndex = 1; pageIndex <= _maximumCatalogPages; pageIndex++)
        {
            var page = await ListPageAsync(new ContentCatalogQuery(PageIndex: pageIndex), cancellationToken)
                .ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                return items;
            }

            items.AddRange(page.Items);
            if (items.Count >= page.Total)
            {
                return items;
            }
        }

        throw new InvalidDataException("ContentServer catalog exceeds the client paging limit.");
    }

    public Task<ContentServerPageResult<ContentCatalogItem>> ListPageAsync(ContentCatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Validate();
        var path = $"api/v1/content?pageIndex={query.PageIndex}&pageSize={query.PageSize}";
        if (query.Type is not null)
        {
            path += $"&type={Uri.EscapeDataString(query.Type)}";
        }

        if (query.Search is not null)
        {
            path += $"&query={Uri.EscapeDataString(query.Search)}";
        }

        return GetPageAsync<ContentCatalogItem>(path, cancellationToken);
    }

    public Task<ContentServerPageResult<ContentCatalogItem>> ListVersionsPageAsync(string contentId,
        int pageIndex = 1, int pageSize = ContentCatalogQuery.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentId);
        ContentCatalogQuery.ValidatePage(pageIndex, pageSize);
        var path = $"api/v1/content/{Uri.EscapeDataString(contentId)}/versions" +
                   $"?pageIndex={pageIndex}&pageSize={pageSize}";
        return GetPageAsync<ContentCatalogItem>(path, cancellationToken);
    }

    public async Task<IReadOnlyList<ContentCatalogItem>> ListVersionsAsync(string contentId,
        CancellationToken cancellationToken = default)
    {
        var items = new List<ContentCatalogItem>();
        for (var pageIndex = 1; pageIndex <= _maximumCatalogPages; pageIndex++)
        {
            var page = await ListVersionsPageAsync(contentId, pageIndex, ContentCatalogQuery.MaximumPageSize,
                cancellationToken).ConfigureAwait(false);
            items.AddRange(page.Items);
            if (page.Items.Count == 0 || items.Count >= page.Total)
            {
                return items;
            }
        }

        throw new InvalidDataException("ContentServer version history exceeds the client paging limit.");
    }

    public async Task<ContentCatalogItem?> FindVersionAsync(string contentId, string version,
        string packageHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageHash);
        for (var pageIndex = 1; pageIndex <= _maximumCatalogPages; pageIndex++)
        {
            var page = await ListVersionsPageAsync(contentId, pageIndex, ContentCatalogQuery.MaximumPageSize,
                cancellationToken).ConfigureAwait(false);
            var match = page.Items.FirstOrDefault(item =>
                string.Equals(item.Version, version, StringComparison.Ordinal) &&
                string.Equals(item.PackageHash, packageHash, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }

            if (page.Items.Count == 0 || page.PageIndex * page.PageSize >= page.Total)
            {
                return null;
            }
        }

        throw new InvalidDataException("ContentServer version history exceeds the client paging limit.");
    }

    public IReadOnlyList<ContentServerModPackage> ListMods() => RunSync(ListModsAsync);

    public ContentServerModPackage? FindMod(string modId, string version) =>
        RunSync(cancellationToken => FindModAsync(modId, version, cancellationToken));

    public LocalModPackageEntry DownloadMod(ContentServerModPackage package, LocalModRepository repository) =>
        RunSync(cancellationToken => DownloadModAsync(package, repository, cancellationToken));

    private async Task<IReadOnlyList<ContentServerModPackage>> ListModsAsync(CancellationToken cancellationToken)
    {
        var items = new List<ContentServerModPackage>();
        for (var pageIndex = 1; pageIndex <= _maximumCatalogPages; pageIndex++)
        {
            var page = await GetPageAsync<ContentServerModPackage>(
                $"api/v1/mods?pageIndex={pageIndex}&pageSize=10", cancellationToken).ConfigureAwait(false);
            if (page.Items.Count == 0)
            {
                return items;
            }

            items.AddRange(page.Items);
            if (items.Count >= page.Total)
            {
                return items;
            }
        }

        throw new InvalidDataException("ContentServer mod catalog exceeds the client paging limit.");
    }

    public async Task<ContentServerModPackage?> FindModAsync(string modId, string version,
        CancellationToken cancellationToken = default)
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
        var result = await ReadJsonAsync<ContentServerResponse<ContentServerModPackage>>(response,
            cancellationToken).ConfigureAwait(false);
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
        EnsurePackageSize(response);
        await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return repository.AddPackageExact(content, package.ModId, package.Version, package.PackageHash);
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
        EnsurePackageSize(response);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        if (!Enum.TryParse<ContentPackageType>(item.Type, false, out var expectedType) ||
            expectedType.ToString() != item.Type)
        {
            throw new InvalidDataException("ContentServer metadata contains an invalid package type.");
        }

        return await cache.ImportExactAsync(stream, expectedType, item.Identifier, item.Version,
            item.PackageHash, cancellationToken).ConfigureAwait(false);
    }

    private static void EnsurePackageSize(HttpResponseMessage response)
    {
        if (response.Content.Headers.ContentLength > ContentPackageCache.MaximumPhysicalBytes)
        {
            throw new InvalidDataException("Content package exceeds the client size limit.");
        }
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<ContentServerPageResult<T>> GetPageAsync<T>(string path,
        CancellationToken cancellationToken)
    {
        using var httpResponse = await _httpClient.GetAsync(path, HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        httpResponse.EnsureSuccessStatusCode();
        var response = await ReadJsonAsync<ContentServerResponse<ContentServerPage<T>>>(httpResponse,
            cancellationToken).ConfigureAwait(false);
        if (response?.Success != true || response.Data is null || response.Data.Items is null ||
            response.Data.Total < 0 || response.Data.PageIndex < 1 || response.Data.PageSize < 1)
        {
            throw new InvalidDataException("ContentServer returned an invalid paged response.");
        }

        return new ContentServerPageResult<T>(response.Data.Items, response.Data.Total,
            response.Data.PageIndex, response.Data.PageSize);
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaximumJsonResponseBytes)
        {
            throw new InvalidDataException("ContentServer JSON response exceeds the client size limit.");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var bytes = new byte[64 * 1024];
        while (true)
        {
            var count = await input.ReadAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            if (buffer.Length + count > MaximumJsonResponseBytes)
            {
                throw new InvalidDataException("ContentServer JSON response exceeds the client size limit.");
            }

            await buffer.WriteAsync(bytes.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
        }

        buffer.Position = 0;
        return await JsonSerializer.DeserializeAsync<T>(buffer, JsonSerializerOptions.Web, cancellationToken)
            .ConfigureAwait(false);
    }

    private static T RunSync<T>(Func<CancellationToken, Task<T>> action) =>
        action(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
}

public sealed record ContentServerHealth(string Name, string Version);

public sealed record ContentCatalogQuery(string? Type = null, string? Search = null, int PageIndex = 1,
    int PageSize = ContentCatalogQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;

    internal void Validate()
    {
        ValidatePage(PageIndex, PageSize);
        if (Type is not null && string.IsNullOrWhiteSpace(Type))
        {
            throw new ArgumentException("Content type filters cannot be blank.", nameof(Type));
        }

        if (Search is not null && string.IsNullOrWhiteSpace(Search))
        {
            throw new ArgumentException("Content search filters cannot be blank.", nameof(Search));
        }
    }

    internal static void ValidatePage(int pageIndex, int pageSize)
    {
        if (pageIndex < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        }

        if (pageSize is < 1 or > MaximumPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }
    }
}

public sealed record ContentServerPageResult<T>(IReadOnlyList<T> Items, int Total, int PageIndex, int PageSize);

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
