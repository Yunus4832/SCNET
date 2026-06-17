using System.Net.Http.Json;

namespace Game.Modding;

public sealed class ModServerClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _disposeClient;

    public ModServerClient(string repositoryUrl, HttpClient? httpClient = null)
    {
        RepositoryUrl = NormalizeRepositoryUrl(repositoryUrl);
        _disposeClient = httpClient == null;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.BaseAddress = new Uri($"{RepositoryUrl}/");
    }

    public string RepositoryUrl { get; }

    public async Task<IReadOnlyList<ModRepositoryPackage>> ListPackagesAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetFromJsonAsync<ModRepositoryListResponse>("api/v1/mods", cancellationToken);
        return response?.Items ?? [];
    }

    public async Task<IReadOnlyList<ModRepositoryPackage>> ListPackagesByModIdAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        var response = await _httpClient.GetFromJsonAsync<ModRepositoryModResponse>(
            $"api/v1/mods/{Uri.EscapeDataString(modId)}",
            cancellationToken);
        return response?.Items ?? [];
    }

    public async Task<ModRepositoryPackage?> FindPackageAsync(
        string modId,
        string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        using var response = await _httpClient.GetAsync(
            $"api/v1/mods/{Uri.EscapeDataString(modId)}/versions/{Uri.EscapeDataString(version)}",
            cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ModRepositoryPackage>(cancellationToken: cancellationToken);
    }

    public async Task<LocalModPackageEntry> DownloadPackageAsync(
        ModRepositoryPackage package,
        LocalModRepository repository,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(repository);

        var existing = repository.FindByHash(package.PackageHash);
        if (existing != null)
        {
            return existing;
        }

        using var response = await _httpClient.GetAsync(package.DownloadUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return repository.AddOrUpdatePackage(package.PackageHash, content, $"{package.PackageHash}.scpak");
    }

    public void Dispose()
    {
        if (_disposeClient)
        {
            _httpClient.Dispose();
        }
    }

    public static string NormalizeRepositoryUrl(string repositoryUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUrl);
        return repositoryUrl.Trim().TrimEnd('/');
    }

    private sealed class ModRepositoryListResponse
    {
        public List<ModRepositoryPackage> Items { get; set; } = [];
    }

    private sealed class ModRepositoryModResponse
    {
        public string ModId { get; set; } = string.Empty;

        public List<ModRepositoryPackage> Items { get; set; } = [];
    }
}
