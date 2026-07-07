namespace Game.Modding;

public sealed class ModPackageRequirement
{
    public string ModId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string? PackageHash { get; set; }
}
