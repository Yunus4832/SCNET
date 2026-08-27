namespace Game;

public static class GamePaths
{
    private static readonly string _gameDataPath = RunPath.GameDataPath;

    public static string External => RunPath.ExternalPath;

    public static string Config => RunPath.ConfigPath;

    public static string ScreenCaptures => $"{External}/ScreenCapture";

    public static string Mods => $"{External}/Mods";

    public static string Worlds => $"{_gameDataPath}/Worlds";

    public static string CharacterSkins => $"{_gameDataPath}/CharacterSkins";

    public static string FurniturePacks => $"{_gameDataPath}/FurniturePacks";

    public static string BlockTextures => $"{_gameDataPath}/TexturePacks";

    public static string ModCache => $"{_gameDataPath}/ModsCache";

    public static string Logs => $"{External}/Logs";

    public static string RunningSettingFile => "config:RunningSetting.xml";

    public static string SessionInfoFile => "config:SessionInfo.xml";

    public static string SettingsFile => "config:Settings.xml";

    public static string GlobalModProfileFile => "config:ModProfile.xml";

    public static string SessionProfilesDirectory => Storage.CombinePaths(Config, "SessionProfiles");

    public static string LocalModsImportStateFile => "config:LocalModsImportState.xml";

    public static string InstalledContentFile => "config:InstalledContent.json";

    public static string NetChunksTempFile => "config:NetChunks.tmp";
}
