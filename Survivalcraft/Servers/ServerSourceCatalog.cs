using ServerSource.Protocol;

namespace Game.Servers;

public sealed class ServerSourceCatalog(
    ServerDirectoryService serverDirectory,
    ServerSourceProtocolClient protocolClient,
    ServerDiscoveryService serverDiscovery)
{
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
