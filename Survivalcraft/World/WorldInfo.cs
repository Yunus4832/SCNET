namespace Game.World;

public class WorldInfo
{
    public string DirectoryName = string.Empty;

    public bool IsNetProject;

    public DateTime LastSaveTime;

    public readonly List<CharacterInfo> CharacterInfos = [];

    public string ProjectFormatVersion = string.Empty;

    public long Size;

    public readonly WorldSettings WorldSettings = new();

    public GameMode? GameModeOverride;
}
