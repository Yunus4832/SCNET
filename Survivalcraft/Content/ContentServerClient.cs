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
        var response = await _httpClient.GetFromJsonAsync<ContentServerResponse<ContentCatalogResponse>>(
            "api/v1/content", cancellationToken).ConfigureAwait(false);
        return response?.Data?.Items ?? [];
    }

    public async Task<byte[]> DownloadAsync(ContentCatalogItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return await _httpClient.GetByteArrayAsync(item.DownloadUrl, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }

    private sealed class ContentCatalogResponse
    {
        public List<ContentCatalogItem> Items { get; init; } = [];
    }
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
