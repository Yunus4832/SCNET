namespace Game.World.Presentation;

public static class WorldSettingsText
{
    public static string FormatOffset(float value)
    {
        if (value != 0f)
        {
            return (value >= 0f ? "+" : "") + value;
        }

        return Localization.ClientLanguage.Catalog.Get("WorldOptionsScreen", 6);
    }
}
