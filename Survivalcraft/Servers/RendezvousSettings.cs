namespace Game.Servers;

public sealed record RendezvousSettings
{
    public IReadOnlyList<InstalledRendezvousService> Services { get; init; } = [];
}
