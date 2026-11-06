namespace Game.Managers;

public static class AppConfigManager
{
    public static readonly Dictionary<string, string> Values = new();

    public static void Set(string key, string value)
    {
        Values[key] = value;
    }
}
