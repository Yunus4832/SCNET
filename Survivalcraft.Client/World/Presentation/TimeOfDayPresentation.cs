namespace Game.World.Presentation;

public static class TimeOfDayPresentation
{
    public static string GetTimeOfDayText(float timeOfDay)
    {
        return timeOfDay switch
        {
            >= 0f and < 0.25f => Localization.ClientLanguage.Catalog.Get(ComponentGui.TypeName, 18),
            >= 0.25f and < 0.5f => Localization.ClientLanguage.Catalog.Get(ComponentGui.TypeName, 15),
            >= 0.5f and < 0.75f => Localization.ClientLanguage.Catalog.Get(ComponentGui.TypeName, 16),
            _ => Localization.ClientLanguage.Catalog.Get(ComponentGui.TypeName, 17)
        };
    }
}
