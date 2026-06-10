namespace Game;

public static class GamePaths
{
    private static readonly string _gameDataPath = RunPath.GameDataPath;

    public static string External => RunPath.ExternalPath;

    public static string Config => RunPath.ConfigPath;

    public static string ScreenCaptures => $"{External}/ScreenCapture";

    public static string Mods => $"{External}/NetMods";

    public static string Worlds => $"{_gameDataPath}/Worlds";

    public static string UserData => $"{_gameDataPath}/UserId.dat";

    public static string CharacterSkins => $"{_gameDataPath}/CharacterSkins";

    public static string FurniturePacks => $"{_gameDataPath}/FurniturePacks";

    public static string BlockTextures => $"{_gameDataPath}/TexturePacks";

    public static string CommunityContentCache => $"{_gameDataPath}/CommunityContentCache.xml";

    public static string ModCache => $"{_gameDataPath}/ModsCache";

    public static string Logs => $"{External}/Logs";

    public static string SettingsFile => "config:Settings.xml";
}
