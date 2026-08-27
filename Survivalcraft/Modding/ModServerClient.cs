using System.Net.Http.Json;

using Game.Content;

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

    public IReadOnlyList<ModRepositoryPackage> ListPackages()
    {
        return RunSync(ListPackagesAsync);
    }

    public IReadOnlyList<ModRepositoryPackage> ListPackagesByModId(string modId)
    {
        return RunSync(cancellationToken => ListPackagesByModIdAsync(modId, cancellationToken));
    }

    public ModRepositoryPackage? FindPackage(string modId, string version)
    {
        return RunSync(cancellationToken => FindPackageAsync(modId, version, cancellationToken));
    }

    public LocalModPackageEntry DownloadPackage(
        ModRepositoryPackage package,
        LocalModRepository repository
    )
    {
        return RunSync(cancellationToken => DownloadPackageAsync(package, repository, cancellationToken));
    }

    private async Task<IReadOnlyList<ModRepositoryPackage>> ListPackagesAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient
            .GetFromJsonAsync<ContentServerResponse<ModRepositoryListResponse>>(
                "api/v1/mods", cancellationToken)
            .ConfigureAwait(false);
        return response?.Data?.Items ?? [];
    }

    private async Task<IReadOnlyList<ModRepositoryPackage>> ListPackagesByModIdAsync(
        string modId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        var response = await _httpClient
            .GetFromJsonAsync<ContentServerResponse<ModRepositoryModResponse>>(
                $"api/v1/mods/{Uri.EscapeDataString(modId)}",
                cancellationToken)
            .ConfigureAwait(false);
        return response?.Data?.Items ?? [];
    }

    private async Task<ModRepositoryPackage?> FindPackageAsync(
        string modId,
        string version,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        using var response = await _httpClient
            .GetAsync(
                $"api/v1/mods/{Uri.EscapeDataString(modId)}/versions/{Uri.EscapeDataString(version)}",
                cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content
            .ReadFromJsonAsync<ContentServerResponse<ModRepositoryPackage>>(
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return result?.Data;
    }

    private async Task<LocalModPackageEntry> DownloadPackageAsync(
        ModRepositoryPackage package,
        LocalModRepository repository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(repository);

        var existing = repository.FindByHash(package.PackageHash);
        if (existing != null)
        {
            return existing;
        }

        using var response = await _httpClient
            .GetAsync(package.DownloadUrl, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return repository.AddOrUpdatePackage(content, $"{package.PackageHash}.scpak");
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

    private static T RunSync<T>(Func<CancellationToken, Task<T>> action)
    {
        return action(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private sealed class ModRepositoryListResponse
    {
        public List<ModRepositoryPackage> Items { get; init; } = [];
    }

    private sealed class ModRepositoryModResponse
    {
        public string ModId { get; init; } = string.Empty;

        public List<ModRepositoryPackage> Items { get; init; } = [];
    }
}
