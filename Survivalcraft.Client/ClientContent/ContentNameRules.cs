namespace Game.Managers;

public static class ContentNameRules
{
    public static Exception? Verify(string name)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            return new InvalidOperationException(Localization.ClientLanguage.Catalog.Get("ContentPackageManager", 1));
        }

        return trimmedName.Length > 50
            ? new InvalidOperationException(Localization.ClientLanguage.Catalog.Get("ContentPackageManager", 2))
            : null;
    }
}
