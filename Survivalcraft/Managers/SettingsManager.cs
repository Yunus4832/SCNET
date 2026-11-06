using System.Xml.Linq;

using Game.Configuration;
using Game.FileStorage;

namespace Game.Managers;

public static class SettingsManager
{
    public static ApplicationPreferences Application { get; } = new();

    private static readonly PreferenceStore _store = new(
        GamePaths.SettingsFile,
        [ClientConfiguration.Preferences, HostingConfiguration.Preferences, Application],
        AppConfigManager.Values
    );

    public static void Initialize()
    {
        if (!Storage.DirectoryExists(GamePaths.Config))
        {
            Storage.CreateDirectory(GamePaths.Config);
        }

        LoadSettings();
        var settingsChanged = false;
        if (HostingConfiguration.Preferences.EnsureHttpCommandAccessToken())
        {
            settingsChanged = true;
        }

        if (settingsChanged)
        {
            SaveSettings();
        }

        Window.Deactivated += SaveSettings;
    }

    public static void LoadSettings()
    {
        try
        {
            _store.Load();
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser("Loading settings failed.", e);
        }
    }

    public static void SaveSettings()
    {
        try
        {
            SaveSettingsCore();
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser("Saving settings failed.", e);
        }
    }

    internal static void ReadSettings(XElement root) => _store.Read(root);

    internal static XElement WriteSettings() => _store.Write();

    private static void SaveSettingsCore() => _store.Save();
}
