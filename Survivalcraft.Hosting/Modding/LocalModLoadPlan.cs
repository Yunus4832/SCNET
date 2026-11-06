using Content.Packaging;

namespace Game.Modding;

/// <summary>Resolves exactly the verified local packages required by one effective profile.</summary>
public static class LocalModLoadPlan
{
    public static IReadOnlyList<ModPackageSource> Resolve(
        ModProfile profile,
        string directory,
        Action<string>? log = null
    )
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ModProfileValidation.Validate(profile);
        var cache = new ContentPackageCache(directory);
        var result = new List<ModPackageSource>();
        foreach (var requirement in profile.Packages)
        {
            var entry = cache.Find(requirement.PackageHash);
            if (entry is null ||
                entry.Type != ContentPackageType.Mod ||
                !string.Equals(entry.Identifier, requirement.ModId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(entry.Version, requirement.Version, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(entry.PackageHash, requirement.PackageHash, StringComparison.Ordinal))
            {
                throw new FileNotFoundException(
                    $"Required local Mod {requirement.ModId}@{requirement.Version} ({requirement.PackageHash}) is unavailable."
                );
            }

            var hash = entry.PackageHash;
            log?.Invoke($"Loading verified local Mod {entry.Identifier}@{entry.Version} ({hash}).");
            result.Add(new ModPackageSource(Path.GetFileName(entry.Path), () => cache.OpenValidated(hash)));
        }

        return result;
    }
}
