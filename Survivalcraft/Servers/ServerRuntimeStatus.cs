using Game.Content;
using Game.Network.Packages;

namespace Game.Servers;

public sealed record ServerRuntimeStatus
{
    public ServerAvailability Availability { get; init; }

    public long PingMilliseconds { get; init; }

    public ushort PlayerCount { get; init; }

    public ushort MaxPlayerCount { get; init; }

    public GameMode GameMode { get; init; }

    public int ProtocolVersion { get; init; }

    public float TimeOfDay { get; init; }

    public Season Season { get; init; }

    public float TimeOfSeason { get; init; }

    public ModProfile? RequiredModProfile { get; init; }

    public IReadOnlyList<ContentRepository> TemporaryRepositories { get; init; } = [];

    public static ServerRuntimeStatus FromPackage(ServerInfoPackage package, long pingMilliseconds)
    {
        return new ServerRuntimeStatus
        {
            Availability = ServerAvailability.Available,
            PingMilliseconds = pingMilliseconds,
            GameMode = package.GameMode,
            MaxPlayerCount = package.MaxPlayerCount,
            PlayerCount = package.ClientCount,
            ProtocolVersion = package.ProtocolVersion,
            TimeOfDay = package.TimeOfDay,
            TemporaryRepositories = package.TemporaryRepositories,
            RequiredModProfile = package.RequiredModProfile,
            Season = package.Season,
            TimeOfSeason = package.TimeOfSeason
        };
    }
}
