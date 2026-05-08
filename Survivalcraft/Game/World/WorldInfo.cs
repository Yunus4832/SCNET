namespace Game;

public class WorldInfo
{
    public string DirectoryName = string.Empty;

    public bool IsNetProject;

    public DateTime LastSaveTime;

    public readonly List<PlayerInfo> PlayerInfos = [];

    public string SerializationVersion = string.Empty;

    public long Size;

    public readonly WorldSettings WorldSettings = new();
}
