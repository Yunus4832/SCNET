namespace Game.Managers;

public static class StringsManager
{
    public static string GetString(string name)
    {
        return LanguageControl.Get("Strings", name);
    }

    public static void LoadStrings()
    {
    }
}
