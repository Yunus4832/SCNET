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

    public static bool PrepareRemoteSessionIfNeeded(
        SessionInfo remoteSession,
        ModProfile? requiredProfile,
        Action<string>? log = null)
    {
        if (requiredProfile is not { Packages.Count: > 0 })
        {
            return false;
        }

        var sessionProfile = ModProfileManager.CreateSessionProfile(string.Empty, requiredProfile);
        var existingProfile = ModProfileManager.LoadSessionProfile(RunningSettingManager.Current.ActiveSessionId);
        var downloadedAny = ModProfileResolver.EnsurePackagesAvailable(
            sessionProfile,
            Storage.GetSystemPath(GamePaths.ModCache),
            log);
        if (AreEquivalent(existingProfile, sessionProfile) && !downloadedAny)
        {
            return false;
        }

        GameExitManager.RequestRestart(remoteSession, sessionProfile);
        return true;
    }

    private static bool AreEquivalent(ModProfile? left, ModProfile right)
    {
        var leftPackages = (left?.Packages ?? [])
            .Select(package => $"{package.ModId.Trim()}@{package.Version.Trim()}#{package.PackageHash?.Trim() ?? string.Empty}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var rightPackages = (right.Packages ?? [])
            .Select(package => $"{package.ModId.Trim()}@{package.Version.Trim()}#{package.PackageHash?.Trim() ?? string.Empty}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return string.Equals(left?.RepositoryUrl, right.RepositoryUrl, StringComparison.OrdinalIgnoreCase) &&
               leftPackages.SequenceEqual(rightPackages, StringComparer.OrdinalIgnoreCase);
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
