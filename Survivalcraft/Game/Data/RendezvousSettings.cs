using Game.Servers;

namespace Game;

public sealed record RendezvousSettings
{
    public IReadOnlyList<InstalledRendezvousService> Services { get; init; } = [];
}
