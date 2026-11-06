namespace Game.Modding;

public static class ModProfileFingerprint
{
    public static string Compute(ModProfile? profile)
    {
        if (profile is not null) { ModProfileValidation.Validate(profile); }

        var lines = (profile?.Packages ?? [])
            .Select(package => $"{package.ModId.Trim()}|{package.Version.Trim()}|{package.PackageHash.Trim()}")
            .OrderBy(line => line, StringComparer.OrdinalIgnoreCase);
        return HashUtils.ComputeSha256(string.Join('\n', lines));
    }
}
