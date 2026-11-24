using Game.Content;
using Game.FileStorage;

namespace Game.Modding;

public static class ModRestartHelper
{
    public static RemoteModSessionPreparation PrepareRemoteSession(
        SessionInfo remoteSession,
        ModProfile? requiredProfile,
        IReadOnlyList<ContentRepository> temporaryRepositories,
        CancellationToken cancellationToken,
        Action<string>? log = null
    )
    {
        ArgumentNullException.ThrowIfNull(remoteSession);
        cancellationToken.ThrowIfCancellationRequested();

        var profile = requiredProfile ?? new ModProfile();
        var repositoryPath = Storage.GetSystemPath(GamePaths.ContentPackageCache);
        var downloadedAny = false;
        if (profile.Packages.Count > 0)
        {
            var scopeId = Guid.NewGuid();
            var context = ContentSourceContext.Session(
                scopeId,
                temporaryRepositories,
                ContentRepositoryManager.Current.Snapshot()
            );
            try
            {
                downloadedAny = ModProfileResolver.EnsurePackagesAvailable(
                    profile,
                    repositoryPath,
                    context,
                    log,
                    cancellationToken,
                    Localization.ClientLanguage.Resolver
                );
            }
            finally { ContentServerClientPool.Shared.RemoveScope(scopeId); }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var plan = ModLoadPlan.ReadClientProfile(profile, repositoryPath, log);
        if (ModProfileIdentity.AreEquivalent(CurrentModRuntime.Value?.LoadedProfile, plan.ClientProfile))
        {
            return RemoteModSessionPreparation.Ready();
        }

        return RemoteModSessionPreparation.RestartRequired(
            remoteSession,
            plan.ClientProfile,
            CreateRestartReason(plan.ClientProfile, downloadedAny)
        );
    }

    public static RemoteModSessionPreparation PrepareWorldSession(
        WorldInfo worldInfo,
        Action<string>? log = null,
        SessionInfo? session = null
    )
    {
        ArgumentNullException.ThrowIfNull(worldInfo);

        var worldSession = session ?? SessionInfoManager.CreateWorldSession(worldInfo);
        var desiredProfile = ModProfileManager.LoadRequiredWorldProfile(worldInfo.DirectoryName);
        return PrepareWorldProfile(worldSession, desiredProfile, log);
    }

    /// <summary>在创建存档前准备玩家选定的模组组合，并保留跨重启的创建请求。</summary>
    public static RemoteModSessionPreparation PrepareWorldCreation(
        SessionInfo creationSession,
        ModProfile desiredProfile,
        Action<string>? log = null
    )
    {
        ArgumentNullException.ThrowIfNull(creationSession.WorldCreation);
        return PrepareWorldProfile(creationSession, desiredProfile, log);
    }

    private static RemoteModSessionPreparation PrepareWorldProfile(
        SessionInfo worldSession,
        ModProfile desiredProfile,
        Action<string>? log
    )
    {
        var repositoryPath = Storage.GetSystemPath(GamePaths.ContentPackageCache);
        var downloadedAny = desiredProfile.Packages.Count > 0 &&
                            ModProfileResolver.EnsurePackagesAvailable(
                                desiredProfile,
                                repositoryPath,
                                log,
                                Localization.ClientLanguage.Resolver
                            );
        var plan = ModLoadPlan.Read(desiredProfile, repositoryPath, log);
        if (ModProfileIdentity.AreEquivalent(CurrentModRuntime.Value?.LoadedProfile, plan.ClientProfile))
        {
            return RemoteModSessionPreparation.Ready();
        }

        return RemoteModSessionPreparation.RestartRequired(
            worldSession,
            plan.FullProfile,
            CreateWorldRestartReason(plan.ClientProfile, downloadedAny)
        );
    }

    private static string CreateRestartReason(ModProfile profile, bool downloadedAny)
    {
        if (profile.Packages.Count == 0)
        {
            return Localization.ClientLanguage.Catalog.Get("ModRestart", "ServerNone");
        }

        var packages = profile.Packages
            .OrderBy(package => package.ModId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .Select(package => $"{package.ModId}@{package.Version}");
        var prefix = Localization.ClientLanguage.Catalog.Get(
            "ModRestart",
            downloadedAny ? "ServerDownloaded" : "ServerSwitch"
        );
        return $"{prefix}\n{string.Join('\n', packages)}";
    }

    private static string CreateWorldRestartReason(ModProfile profile, bool downloadedAny)
    {
        if (profile.Packages.Count == 0)
        {
            return Localization.ClientLanguage.Catalog.Get("ModRestart", "WorldNone");
        }

        var packages = profile.Packages
            .OrderBy(package => package.ModId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .Select(package => $"{package.ModId}@{package.Version}");
        var prefix = Localization.ClientLanguage.Catalog.Get(
            "ModRestart",
            downloadedAny ? "WorldDownloaded" : "WorldSwitch"
        );
        return $"{prefix}\n{string.Join('\n', packages)}";
    }
}

public sealed class RemoteModSessionPreparation
{
    private RemoteModSessionPreparation(
        bool requiresRestart,
        SessionInfo? remoteSession,
        ModProfile? sessionProfile,
        string restartReason
    )
    {
        RequiresRestart = requiresRestart;
        RemoteSession = remoteSession;
        SessionProfile = sessionProfile;
        RestartReason = restartReason;
    }

    public bool RequiresRestart { get; }

    public SessionInfo? RemoteSession { get; }

    public ModProfile? SessionProfile { get; }

    public string RestartReason { get; }

    public static RemoteModSessionPreparation Ready()
    {
        return new RemoteModSessionPreparation(false, null, null, string.Empty);
    }

    public static RemoteModSessionPreparation RestartRequired(
        SessionInfo remoteSession,
        ModProfile sessionProfile,
        string restartReason
    )
    {
        return new RemoteModSessionPreparation(true, remoteSession, sessionProfile, restartReason);
    }
}
