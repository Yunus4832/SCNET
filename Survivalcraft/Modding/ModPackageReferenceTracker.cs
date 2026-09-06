namespace Game.Modding;

public static class ModPackageReferenceTracker
{
    public static IReadOnlyList<ModPackageRequirement> GetReferencedRequirements()
    {
        return EnumerateProfiles().SelectMany(profile => profile.Packages)
            .DistinctBy(requirement => requirement.PackageHash, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyList<ModPackageRequirement> GetCurrentRuntimeRequirements()
    {
        return (CurrentModRuntime.Value?.EffectiveProfile.Packages ?? [])
            .ToArray();
    }

    public static bool IsReferenced(LocalModPackageEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return IsReferenced(entry, EnumerateProfiles());
    }

    internal static bool IsReferenced(LocalModPackageEntry entry, IEnumerable<ModProfile> profiles)
    {
        return profiles.Any(profile => profile.Packages.Any(requirement =>
            string.Equals(requirement.ModId, entry.ModId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(requirement.Version, entry.Version, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(requirement.PackageHash, entry.PackageHash, StringComparison.Ordinal)));
    }

    private static IEnumerable<ModProfile> EnumerateProfiles()
    {
        yield return ModProfileManager.LoadGlobalProfile();
        if (CurrentModRuntime.Value?.EffectiveProfile is { } runtimeProfile)
        {
            yield return runtimeProfile;
        }

        if (Storage.DirectoryExists(GamePaths.SessionProfilesDirectory))
        {
            foreach (var fileName in Storage.ListFileNames(GamePaths.SessionProfilesDirectory))
            {
                var profile = ModProfileManager.LoadSessionProfile(Path.GetFileNameWithoutExtension(fileName));
                if (profile is not null)
                {
                    yield return profile;
                }
            }
        }

        if (Storage.DirectoryExists(GamePaths.Worlds))
        {
            foreach (var directoryName in Storage.ListDirectoryNames(GamePaths.Worlds))
            {
                var profile = ModProfileManager.LoadWorldProfile(
                    Storage.CombinePaths(GamePaths.Worlds, directoryName));
                if (profile is not null)
                {
                    yield return profile;
                }
            }
        }
    }
}
