namespace Game.Servers;

public sealed record ServerDirectoryState
{
    public IReadOnlyList<StoredServerEntry> MyServers { get; init; } = [];

    public IReadOnlyList<StoredServerEntry> Favorites { get; init; } = [];

    public IReadOnlyList<StoredServerEntry> RecentServers { get; init; } = [];

    public IReadOnlyList<ServerSourceSubscription> Subscriptions { get; init; } = [];
}
