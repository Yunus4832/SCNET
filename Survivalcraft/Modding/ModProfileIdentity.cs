namespace Game.Modding;

public static class ModProfileIdentity
{
    public static bool AreEquivalent(ModProfile? first, ModProfile? second)
    {
        var left = CreateIdentities(first);
        var right = CreateIdentities(second);
        return left.SequenceEqual(right);
    }

    private static IReadOnlyList<PackageIdentity> CreateIdentities(ModProfile? profile)
    {
        return (profile?.Packages ?? [])
            .Select(package => new PackageIdentity(package.ModId.Trim().ToLowerInvariant(),
                package.Version.Trim(), package.PackageHash.Trim()))
            .OrderBy(identity => identity.ModId, StringComparer.Ordinal)
            .ThenBy(identity => identity.Version, StringComparer.Ordinal)
            .ThenBy(identity => identity.PackageHash, StringComparer.Ordinal)
            .ToArray();
    }

    private sealed record PackageIdentity(string ModId, string Version, string PackageHash);
}
