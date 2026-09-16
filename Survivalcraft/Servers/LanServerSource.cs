namespace Game.Servers;

public sealed class LanServerSource(ServerDiscoveryService discoveryService) : IServerSource
{
    public string Id => ServerSourceIds.Lan;

    public string Name => "LAN";

    public ServerSourceKind Kind => ServerSourceKind.Lan;

    public Task<IReadOnlyList<ServerItem>> LoadAsync(CancellationToken cancellationToken)
    {
        return discoveryService.DiscoverLanAsync(TimeSpan.FromMilliseconds(750), cancellationToken);
    }
}
