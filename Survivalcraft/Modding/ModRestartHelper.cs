namespace Game.Modding;

public static class ModRestartHelper
{
    public static RemoteModSessionPreparation PrepareRemoteSession(
        SessionInfo remoteSession,
        ModProfile? requiredProfile,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(remoteSession);

        if (requiredProfile is not { Packages.Count: > 0 })
        {
            return RemoteModSessionPreparation.Ready();
        }

        var sessionProfile = ModProfileManager.CreateSessionProfile(string.Empty, requiredProfile);
        var downloadedAny = ModProfileResolver.EnsurePackagesAvailable(
            sessionProfile,
            Storage.GetSystemPath(GamePaths.ModCache),
            log);
        if (AreEquivalent(CurrentModRuntime.Value?.EffectiveProfile, sessionProfile))
        {
            return RemoteModSessionPreparation.Ready();
        }

        return RemoteModSessionPreparation.RestartRequired(
            remoteSession,
            sessionProfile,
            CreateRestartReason(sessionProfile, downloadedAny));
    }

    public static RemoteModSessionPreparation PrepareWorldSession(
        WorldInfo worldInfo,
        Action<string>? log = null)
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
                                Storage.GetSystemPath(GamePaths.ModCache),
                                log);
        if (AreEquivalent(CurrentModRuntime.Value?.EffectiveProfile, desiredProfile))
        {
            return RemoteModSessionPreparation.Ready();
        }

        return RemoteModSessionPreparation.RestartRequired(
            worldSession,
            sessionProfile,
            CreateWorldRestartReason(desiredProfile, downloadedAny));
    }

    private static bool AreEquivalent(ModProfile? left, ModProfile right)
    {
        var leftPackages = (left?.Packages ?? [])
            .Select(package => $"{package.ModId.Trim()}@{package.Version.Trim()}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rightPackages = (right.Packages ?? [])
            .Select(package => $"{package.ModId.Trim()}@{package.Version.Trim()}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return leftPackages.SequenceEqual(rightPackages, StringComparer.OrdinalIgnoreCase);
    }

    private static string CreateRestartReason(ModProfile profile, bool downloadedAny)
    {
        var packages = profile.Packages
            .OrderBy(package => package.ModId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .Select(package => $"{package.ModId}@{package.Version}");
        var prefix = downloadedAny
            ? "已下载服务器需要的模组，需要重启后启用："
            : "服务器需要切换到指定模组列表，需要重启后启用：";
        return $"{prefix}\n{string.Join('\n', packages)}";
    }

    private static string CreateWorldRestartReason(ModProfile profile, bool downloadedAny)
    {
        if (profile.Packages.Count == 0)
        {
            return "该世界不启用模组，需要重启后卸载当前模组。";
        }

        var packages = profile.Packages
            .OrderBy(package => package.ModId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .Select(package => $"{package.ModId}@{package.Version}");
        var prefix = downloadedAny
            ? "已下载该世界需要的模组，需要重启后启用："
            : "该世界需要切换到指定模组列表，需要重启后启用：";
        return $"{prefix}\n{string.Join('\n', packages)}";
    }

}

public sealed class RemoteModSessionPreparation
{
    private RemoteModSessionPreparation(
        bool requiresRestart,
        SessionInfo? remoteSession,
        ModProfile? sessionProfile,
        string restartReason)
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
        string restartReason)
    {
        return new RemoteModSessionPreparation(true, remoteSession, sessionProfile, restartReason);
    }
}
