using Game.Content;

namespace Game.Network.Snapshots;

public static class ServerInfoPackageBuilder
{
    public static ServerInfoPackage Capture()
    {
        var project = GameManager.Project ?? throw new InvalidOperationException("GameManager is not ready.");
        var settings = project.FindSubsystem<SubsystemGameInfo>(true)!.WorldSettings;
        var seasons = project.FindSubsystem<SubsystemSeasons>(true)!;
        var profile = CurrentModRuntime.Value?.CreateServerRequiredProfile();
        if (profile != null) { ModProfileValidation.Validate(profile); }

        return new ServerInfoPackage
        {
            ProtocolVersion = VersionsManager.ProtocolVersion,
            ClientCount = (ushort)CommonLib.Net.ClientCount,
            MaxPlayerCount = settings.MaxOnlinePlayerCount,
            GameMode = ProtocolEnum.Encode(settings.GameMode),
            TimeOfDay = project.FindSubsystem<SubsystemTimeOfDay>(true)!.CalculateTimeOfDay(),
            Season = ProtocolEnum.Encode(seasons.Season),
            TimeOfSeason = seasons.TimeOfSeason,
            RequiredModProfile = profile == null
                ? null
                : new RequiredModProfile(
                    profile.Id,
                    profile.Packages.Select(p => new RequiredModPackage(p.ModId, p.Version, p.PackageHash)).ToArray()
                ),
            TemporaryRepositories = TemporaryContentRepositories.Validate(
                ContentRepositoryManager.Current.Snapshot()
                    .Where(repository => repository.IsEnabled)
            ).Select(repository =>
                new ContentRepositoryDescriptor(repository.Id, repository.BaseUrl, repository.Priority)
            ).ToArray()
        };
    }
}
