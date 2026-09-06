using System.Text.RegularExpressions;

using Content.Packaging;

namespace Game.Modding;

public static partial class ModProfileValidation
{
    public const int MaximumRequirements = 256;
    public const int MaximumProfileIdLength = 128;
    public const int MaximumModIdLength = 128;
    public const int MaximumVersionLength = 64;

    public static void Validate(ModProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(profile.Id) || profile.Id.Length > MaximumProfileIdLength)
        {
            throw new ArgumentException("A mod profile ID is required.", nameof(profile));
        }

        if (profile.Packages is null || profile.Packages.Count > MaximumRequirements)
        {
            throw new ArgumentException("A mod profile contains too many requirements.", nameof(profile));
        }

        var modIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requirement in profile.Packages)
        {
            Validate(requirement);
            if (!modIds.Add(requirement.ModId))
            {
                throw new ArgumentException($"Mod profile contains duplicate requirement '{requirement.ModId}'.",
                    nameof(profile));
            }
        }
    }

    public static void Validate(ModPackageRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (requirement.ModId.Length > MaximumModIdLength || !ModIdPattern().IsMatch(requirement.ModId))
        {
            throw new ArgumentException("A mod requirement contains an invalid ModId.", nameof(requirement));
        }

        if (requirement.Version.Length > MaximumVersionLength ||
            !SemanticVersion.TryParse(requirement.Version, out _))
        {
            throw new ArgumentException("A mod requirement contains an invalid semantic version.",
                nameof(requirement));
        }

        if (requirement.PackageHash.Length != 64 || requirement.PackageHash.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A mod requirement requires a canonical SHA-256 PackageHash.",
                nameof(requirement));
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*(?:\\.[a-z0-9][a-z0-9_-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ModIdPattern();
}
