namespace Game.World;

public class WorldInfo
{
    public string DirectoryName = string.Empty;

    public bool IsNetProject;

    public DateTime LastSaveTime;

    public readonly List<CharacterInfo> CharacterInfos = [];

    internal HashSet<string> OfflineCharacterSkinNames { get; } = new(StringComparer.Ordinal);

    public string ProjectFormatVersion = string.Empty;

    public long Size;

    public readonly WorldSettings WorldSettings = new();

    public readonly WorldHostingSettings Hosting = new();

    public GameMode? GameModeOverride;

    public bool UsesCharacterSkin(string name)
    {
        return OfflineCharacterSkinNames.Contains(name) ||
               CharacterInfos.Any(character => character.CharacterSkinName == name);
    }
}
