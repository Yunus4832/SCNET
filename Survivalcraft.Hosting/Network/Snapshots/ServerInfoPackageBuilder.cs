using EntitySystem.Core;

namespace Game.Network.Snapshots;

public static class ServerInfoPackageBuilder
{
    public static ServerInfoResponsePackage Capture(
        Project project,
        ModProfile? profile,
        IEnumerable<ContentRepository> repositories
    )
    {
        var runtime = WorldRuntimeContext.For(project);
        if (!runtime.IsAuthority)
        {
            throw new InvalidOperationException("Server discovery requires an authority world.");
        }

        var settings = project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings;
        var seasons = project.FindSubsystem<SubsystemSeasons>(true)!;
        if (profile != null) { ModProfileValidation.Validate(profile); }

        return new ServerInfoResponsePackage
        {
            ProtocolVersion = GameProtocol.CurrentVersion,
            ClientCount = (ushort)WorldConnections.For(runtime).PlayerCount,
            MaxPlayerCount = settings.MaxOnlinePlayerCount, GameMode = ProtocolEnum.Encode(settings.GameMode),
            TimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>(true)!.CalculateTimeOfDay(),
            Season = ProtocolEnum.Encode(seasons.Season), TimeOfSeason = seasons.TimeOfSeason, RequiredModProfile =
                profile == null
                    ? null
                    : new RequiredModProfile(
                        profile.Id,
                        profile.Packages.Select(p => new RequiredModPackage(p.ModId, p.Version, p.PackageHash))
                            .ToArray()
                    ),
            TemporaryRepositories = TemporaryContentRepositories.Validate(
                repositories
                    .Where(repository => repository.IsEnabled)
            ).Select(repository =>
                new ContentRepositoryDescriptor(repository.Id, repository.BaseUrl, repository.Priority)
            ).ToArray()
        };
    }
}
