namespace Game.ModManager;

public class ModInfo
{
    public readonly List<string> Dependencies = [];

    public string Name = string.Empty;

    public string Version = string.Empty;

    public string ApiVersion = string.Empty;

    public string Description = string.Empty;

    public string ScVersion = string.Empty;

    public string Link = string.Empty;

    public string Author = string.Empty;

    public string PackageName = string.Empty;

    public override int GetHashCode()
    {
        return (PackageName + ApiVersion + Version).GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        return obj is ModInfo && obj.GetHashCode() == GetHashCode();
    }
}
