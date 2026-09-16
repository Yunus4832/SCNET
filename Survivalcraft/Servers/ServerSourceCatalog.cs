using ServerSource.Protocol;

namespace Game.Servers;

public sealed class ServerSourceCatalog
{
    private readonly ServerSourceProtocolClient _protocolClient;
    private readonly ServerDirectoryService _serverDirectory;
    private readonly ServerDiscoveryService _serverDiscovery;

    public ServerSourceCatalog(ServerDirectoryService serverDirectory, ServerSourceProtocolClient protocolClient,
        ServerDiscoveryService serverDiscovery)
    {
        _serverDirectory = serverDirectory;
        _protocolClient = protocolClient;
        _serverDiscovery = serverDiscovery;
    }

    public IReadOnlyList<IServerSource> GetEnabledSources()
    {
        var state = _serverDirectory.Snapshot();
        var sources = new List<IServerSource>
        {
            new StoredServerSource(ServerSourceIds.MyServers, "My Servers", ServerSourceKind.MyServers,
                _serverDirectory, snapshot => snapshot.MyServers),
            new StoredServerSource(ServerSourceIds.Favorites, "Favorites", ServerSourceKind.Favorites,
                _serverDirectory, snapshot => snapshot.Favorites),
            new StoredServerSource(ServerSourceIds.Recent, "Recent", ServerSourceKind.Recent,
                _serverDirectory, snapshot => snapshot.RecentServers),
            new LanServerSource(_serverDiscovery)
        };
        sources.AddRange(state.InstalledSources.Where(source => source.IsEnabled)
            .OrderBy(source => source.Order)
            .Select(source => new HttpServerSource(source, _protocolClient)));
        return sources;
    }
}
