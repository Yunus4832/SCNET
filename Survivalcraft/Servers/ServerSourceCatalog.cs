using ServerSource.Protocol;

namespace Game.Servers;

public sealed class ServerSourceCatalog(
    ServerDirectoryManager serverDirectory,
    ServerSourceProtocolClient protocolClient,
    ServerDiscoveryService serverDiscovery)
{
    private static readonly Lazy<ServerSourceCatalog> _current = new(() =>
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxResponseContentBufferSize = ServerSourceProtocol.MaximumResponseBytes
        };
        return new ServerSourceCatalog(ServerDirectoryManager.Current, new ServerSourceProtocolClient(httpClient),
            new ServerDiscoveryService());
    });

    public static ServerSourceCatalog Current => _current.Value;

    public IReadOnlyList<IServerSource> GetEnabledSources()
    {
        var state = serverDirectory.Snapshot();
        var sources = new List<IServerSource>
        {
            new LocalServerSource(serverDirectory),
            new LanServerSource(serverDiscovery)
        };
        sources.AddRange(state.InstalledSources.Where(source => source.IsEnabled)
            .OrderBy(source => source.Order)
            .Select(source => new HttpServerSource(source, protocolClient)));
        return sources;
    }
}
