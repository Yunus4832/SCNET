namespace Game.Modding;

public sealed class ModProfile
{
    public string Id { get; set; } = "default";

    public List<ModPackageRequirement> Packages { get; set; } = [];
}
