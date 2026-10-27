using Game.Content;
using Game.Network;

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

    public static ServerRuntimeStatus FromPackage(ServerInfoResponsePackage package, long pingMilliseconds)
    {
        return new ServerRuntimeStatus
        {
            Availability = ServerAvailability.Available,
            PingMilliseconds = pingMilliseconds,
            GameMode = ProtocolEnum.Decode<GameMode>(package.GameMode),
            MaxPlayerCount = package.MaxPlayerCount,
            PlayerCount = package.ClientCount,
            ProtocolVersion = package.ProtocolVersion,
            TimeOfDay = package.TimeOfDay,
            TemporaryRepositories = TemporaryContentRepositories.Validate(
                package.TemporaryRepositories.Select((r, i) =>
                    new ContentRepository
                        { Id = r.Id, Name = $"Server Repository {i + 1}", BaseUrl = r.BaseUrl, Priority = r.Priority }
                )
            ),
            RequiredModProfile = RestoreProfile(package.RequiredModProfile),
            Season = ProtocolEnum.Decode<Season>(package.Season),
            TimeOfSeason = package.TimeOfSeason
        };
    }

    private static ModProfile? RestoreProfile(RequiredModProfile? profile)
    {
        if (profile == null) { return null; }

        var value = new ModProfile
        {
            Id = profile.Id,
            Packages = profile.Packages.Select(p => new ModPackageRequirement
                { ModId = p.ModId, Version = p.Version, PackageHash = p.PackageHash }
            ).ToList()
        };
        ModProfileValidation.Validate(value);
        return value;
    }
}
