using Game.Content;

namespace Game.Servers;

public sealed record ServerRuntimeStatus
{
    public ServerAvailability Availability { get; init; }

    public long PingMilliseconds { get; init; }

    public ushort PlayerCount { get; init; }

    public ushort MaxPlayerCount { get; init; }

    public GameMode GameMode { get; init; }

    public string Version { get; init; } = string.Empty;

    public float TimeOfDay { get; init; }

    public Season Season { get; init; }

    public float TimeOfSeason { get; init; }

    public ModProfile? RequiredModProfile { get; init; }

    public IReadOnlyList<ContentRepository> TemporaryRepositories { get; init; } = [];
}
