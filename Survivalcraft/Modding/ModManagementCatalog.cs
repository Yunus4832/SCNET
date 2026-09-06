using Content.Packaging;

namespace Game.Modding;

public sealed record ManagedModItem(
    string ModId,
    string Version,
    string PackageHash,
    LocalModPackageEntry? LocalEntry,
    bool IsGlobal,
    bool IsWorld,
    bool IsRuntime)
{
    public bool IsMissing => LocalEntry is null;

    public ModPackageRequirement ToRequirement()
    {
        return new ModPackageRequirement
        {
            ModId = ModId,
            Version = Version,
            PackageHash = PackageHash
        };
    }
}

public static class ModManagementCatalog
{
    public static IReadOnlyList<ManagedModItem> Build(IEnumerable<LocalModPackageEntry> cached,
        ModProfile globalProfile, IEnumerable<ModProfile> worldProfiles, ModProfile? runtimeProfile)
    {
        ArgumentNullException.ThrowIfNull(cached);
        ArgumentNullException.ThrowIfNull(globalProfile);
        ArgumentNullException.ThrowIfNull(worldProfiles);
        var items = new Dictionary<PackageIdentity, ManagedModItem>();
        foreach (var entry in cached)
        {
            var identity = PackageIdentity.Create(entry.ModId, entry.Version, entry.PackageHash);
            items[identity] = new ManagedModItem(entry.ModId, entry.Version, entry.PackageHash,
                entry, false, false, false);
        }

        AddProfile(items, globalProfile, ProfileScope.Global);
        foreach (var profile in worldProfiles)
        {
            AddProfile(items, profile, ProfileScope.World);
        }

        if (runtimeProfile is not null)
        {
            AddProfile(items, runtimeProfile, ProfileScope.Runtime);
        }

        return items.Values
            .OrderBy(item => item.ModId, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => SemanticVersion.Parse(item.Version))
            .ThenBy(item => item.PackageHash, StringComparer.Ordinal)
            .ToArray();
    }

    public static bool ContainsExact(ModProfile profile, ManagedModItem item)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(item);
        return profile.Packages.Any(requirement =>
            string.Equals(requirement.ModId, item.ModId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(requirement.Version, item.Version, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(requirement.PackageHash, item.PackageHash, StringComparison.Ordinal));
    }

    private static void AddProfile(Dictionary<PackageIdentity, ManagedModItem> items, ModProfile profile,
        ProfileScope scope)
    {
        foreach (var requirement in profile.Packages)
        {
            var identity = PackageIdentity.Create(requirement.ModId, requirement.Version,
                requirement.PackageHash);
            if (!items.TryGetValue(identity, out var item))
            {
                item = new ManagedModItem(requirement.ModId, requirement.Version, requirement.PackageHash,
                    null, false, false, false);
            }

            items[identity] = scope switch
            {
                ProfileScope.Global => item with { IsGlobal = true },
                ProfileScope.World => item with { IsWorld = true },
                ProfileScope.Runtime => item with { IsRuntime = true },
                _ => throw new ArgumentOutOfRangeException(nameof(scope))
            };
        }
    }

    private enum ProfileScope
    {
        Global,
        World,
        Runtime
    }

    private sealed record PackageIdentity(string ModId, string Version, string PackageHash)
    {
        public static PackageIdentity Create(string modId, string version, string packageHash)
        {
            return new PackageIdentity(modId.ToLowerInvariant(), version.ToLowerInvariant(), packageHash);
        }
    }
}
