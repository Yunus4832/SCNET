using Game.Content;

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

        if (requiredProfile is null)
        {
            return RemoteModSessionPreparation.Ready();
        }

        var sessionProfile = ModProfileManager.CreateSessionProfile(string.Empty, requiredProfile);
        var scopeId = Guid.NewGuid();
        var context = ContentSourceContext.Session(
            scopeId,
            temporaryRepositories,
            ContentRepositoryManager.Current.Snapshot()
        );
        bool downloadedAny;
        try
        {
            downloadedAny = sessionProfile.Packages.Count > 0 &&
                            ModProfileResolver.EnsurePackagesAvailable(
                                sessionProfile,
                                Storage.GetSystemPath(GamePaths.ContentPackageCache),
                                context,
                                log,
                                cancellationToken
                            );
        }
        finally
        {
            ContentServerClientPool.Shared.RemoveScope(scopeId);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (ModProfileIdentity.AreEquivalent(CurrentModRuntime.Value?.EffectiveProfile, sessionProfile))
        {
            return RemoteModSessionPreparation.Ready();
        }

        return RemoteModSessionPreparation.RestartRequired(
            remoteSession,
            sessionProfile,
            CreateRestartReason(sessionProfile, downloadedAny)
        );
    }

    public static RemoteModSessionPreparation PrepareWorldSession(
        WorldInfo worldInfo,
        Action<string>? log = null
    )
    {
        ArgumentNullException.ThrowIfNull(worldInfo);

        var worldSession = new SessionInfo
        {
            Target = SessionTarget.World,
            World = worldInfo.DirectoryName
        };
        var desiredProfile = ModProfileManager.ResolveProfileForSessionTarget(worldSession);
        var sessionProfile = ModProfileManager.CreateSessionProfile(string.Empty, desiredProfile);
        var downloadedAny = desiredProfile.Packages.Count > 0 &&
                            ModProfileResolver.EnsurePackagesAvailable(
                                desiredProfile,
                                Storage.GetSystemPath(GamePaths.ContentPackageCache),
                                log
                            );
        if (ModProfileIdentity.AreEquivalent(CurrentModRuntime.Value?.EffectiveProfile, desiredProfile))
        {
            return RemoteModSessionPreparation.Ready();
        }

        return RemoteModSessionPreparation.RestartRequired(
            worldSession,
            sessionProfile,
            CreateWorldRestartReason(desiredProfile, downloadedAny)
        );
    }

    private static string CreateRestartReason(ModProfile profile, bool downloadedAny)
    {
        if (profile.Packages.Count == 0)
        {
            return LanguageManager.Get("ModRestart", "ServerNone");
        }

        var packages = profile.Packages
            .OrderBy(package => package.ModId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .Select(package => $"{package.ModId}@{package.Version}");
        var prefix = LanguageManager.Get("ModRestart", downloadedAny ? "ServerDownloaded" : "ServerSwitch");
        return $"{prefix}\n{string.Join('\n', packages)}";
    }

    private static string CreateWorldRestartReason(ModProfile profile, bool downloadedAny)
    {
        if (profile.Packages.Count == 0)
        {
            return LanguageManager.Get("ModRestart", "WorldNone");
        }

        var packages = profile.Packages
            .OrderBy(package => package.ModId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .Select(package => $"{package.ModId}@{package.Version}");
        var prefix = LanguageManager.Get("ModRestart", downloadedAny ? "WorldDownloaded" : "WorldSwitch");
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
