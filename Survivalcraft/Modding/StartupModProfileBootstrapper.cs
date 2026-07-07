namespace Game.Modding;

public static class StartupModProfileBootstrapper
{
    public static bool EnsureStartupSessionProfile(
        string? activeSessionId,
        SessionInfo startupSession,
        string localRepositoryPath,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(startupSession);

        if (startupSession.Target != SessionTarget.World)
        {
            return false;
        }

        var desiredProfile = ModProfileManager.ResolveProfileForSessionTarget(startupSession);
        var existingProfile = ModProfileManager.LoadSessionProfile(activeSessionId);
        var downloadedAny = ModProfileResolver.EnsurePackagesAvailable(desiredProfile, localRepositoryPath, log);

        if (AreEquivalent(existingProfile, desiredProfile) && !downloadedAny)
        {
            return false;
        }

        var sessionProfile = ModProfileManager.CreateSessionProfile(string.Empty, desiredProfile);
        GameExitManager.RequestRestart(startupSession, sessionProfile);
        return true;
    }

    private static bool AreEquivalent(ModProfile? sessionProfile, ModProfile profile)
    {
        var left = (sessionProfile?.Packages ?? [])
            .Select(package => $"{package.ModId.Trim()}@{package.Version.Trim()}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var right = (profile?.Packages ?? [])
            .Select(package => $"{package.ModId.Trim()}@{package.Version.Trim()}")
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return left.SequenceEqual(right, StringComparer.OrdinalIgnoreCase);
    }
}
