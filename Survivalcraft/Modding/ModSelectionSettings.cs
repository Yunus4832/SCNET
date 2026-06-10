namespace Game.Modding;

public static class ModSelectionSettings
{
    private static readonly HashSet<string> _disabledPackages = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> DisabledPackages => _disabledPackages;

    public static bool IsDisabled(string packageName)
    {
        return !string.IsNullOrWhiteSpace(packageName) && _disabledPackages.Contains(packageName);
    }

    public static void ReplaceDisabledPackages(IEnumerable<string> packageNames)
    {
        _disabledPackages.Clear();
        foreach (var packageName in packageNames)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                continue;
            }

            _disabledPackages.Add(packageName);
        }
    }
}
