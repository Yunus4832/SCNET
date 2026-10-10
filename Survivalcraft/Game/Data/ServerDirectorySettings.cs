using Game.Servers;

namespace Game;

public sealed record ServerDirectorySettings
{
    public IReadOnlyList<StoredServerEntry> LocalServers { get; init; } = [];

    public IReadOnlyList<InstalledServerSource> InstalledSources { get; init; } = [];
}
