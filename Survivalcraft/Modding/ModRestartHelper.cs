namespace Game.Modding;

public static class ModRestartHelper
{
    public static void HandleModDataValidationMessage(string message)
    {
        if (RequiresClientRestart(message))
        {
            GameExitManager.RequestRestart();
        }
    }

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
        var existingProfile = ModProfileManager.LoadSessionProfile(RunningSettingManager.Current.ActiveSessionId);
        var downloadedAny = ModProfileResolver.EnsurePackagesAvailable(
            sessionProfile,
            Storage.GetSystemPath(GamePaths.ModCache),
            log);
        if (AreEquivalent(CurrentModRuntime.Value?.EffectiveProfile, sessionProfile) ||
            (AreEquivalent(existingProfile, sessionProfile) && !downloadedAny))
        {
            return RemoteModSessionPreparation.Ready();
        }

        return RemoteModSessionPreparation.RestartRequired(
            remoteSession,
            sessionProfile,
            CreateRestartReason(sessionProfile, downloadedAny));
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

    private static bool RequiresClientRestart(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (message.Contains("资源包") && message.Contains("校验不通过"))
        {
            return HasClientPayload(message, "[服务端]", "[客户端]");
        }

        if (message.Contains("The resource package") && message.Contains("verification failed"))
        {
            return HasClientPayload(message, "[Server]", "[Client]");
        }

        return message.Contains("Mod验证不通过。请去掉多余的mod或添加服务器所需要的mod");
    }

    private static bool HasClientPayload(string message, string serverMarker, string clientMarker)
    {
        var serverStartIndex = message.IndexOf(serverMarker, StringComparison.Ordinal);
        var clientStartIndex = message.IndexOf(clientMarker, StringComparison.Ordinal);
        if (serverStartIndex < 0 || clientStartIndex < 0 || clientStartIndex <= serverStartIndex)
        {
            return false;
        }

        serverStartIndex += serverMarker.Length;
        if (!string.IsNullOrWhiteSpace(message.Substring(serverStartIndex, clientStartIndex - serverStartIndex)))
        {
            return false;
        }

        clientStartIndex += clientMarker.Length;
        return clientStartIndex <= message.Length &&
               !string.IsNullOrWhiteSpace(message[clientStartIndex..]);
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
