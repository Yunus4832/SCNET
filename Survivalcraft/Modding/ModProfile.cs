namespace Game.Modding;

public sealed class ModProfile
{
    public string Id { get; set; } = "default";

    public string? ContentServerUrl { get; set; }

    public List<ModPackageRequirement> Packages { get; set; } = [];
}
