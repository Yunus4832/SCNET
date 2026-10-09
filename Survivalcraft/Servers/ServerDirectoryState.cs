namespace Game.Servers;

public sealed record ServerDirectoryState
{
    public IReadOnlyList<StoredServerEntry> LocalServers { get; init; } = [];

    public IReadOnlyList<InstalledServerSource> InstalledSources { get; init; } = [];
}
