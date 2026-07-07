namespace Game.Managers;

public static class StringsManager
{
    public static string GetString(string name)
    {
        return LanguageManager.Get("Strings", name);
    }

    public static string GetString(params object?[] parts)
    {
        return GetString(string.Join("_", parts.Select(part => part?.ToString() ?? string.Empty)));
    }

    public static void LoadStrings()
    {
    }
}
