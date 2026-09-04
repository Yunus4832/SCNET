using System.Security.Cryptography;
using System.Xml.Linq;

using Engine.Serialization;

using EntitySystem.XmlUtilities;

using Game.Network;

namespace Game.Managers;

public static class SettingsManager
{
    public static Settings Current { get; } = new();

    public static event Action? BrightnessChanged;

    internal static void NotifyBrightnessChanged()
    {
        BrightnessChanged?.Invoke();
    }

    public static void Initialize()
    {
        var screenWidth = RunMode.Value is RunModeType.HeadlessServer ? 1280 : Window.ScreenSize.X;
        var screenHeight = RunMode.Value is RunModeType.HeadlessServer ? 720 : Window.ScreenSize.Y;
        var isWideScreen = screenWidth / (float)screenHeight > 1.33333337f;
        Current.ScreenLayout2 = isWideScreen ? ScreenLayout.DoubleVertical : ScreenLayout.DoubleHorizontal;
        Current.ScreenLayout3 = isWideScreen ? ScreenLayout.TripleVertical : ScreenLayout.TripleHorizontal;

        if (!Storage.DirectoryExists(GamePaths.Config))
        {
            Storage.CreateDirectory(GamePaths.Config);
        }

        LoadSettings();
        var settingsChanged = false;
        if (EnsureMultiplayerClientId(Current))
        {
            settingsChanged = true;
        }

        if (EnsureHttpCommandAccessToken(Current))
        {
            settingsChanged = true;
        }

        if (settingsChanged)
        {
            SaveSettings();
        }

        Window.Deactivated += SaveSettings;
    }

    internal static bool EnsureMultiplayerClientId(Settings settings)
    {
        if (settings.MultiplayerClientId != Guid.Empty)
        {
            return false;
        }

        settings.MultiplayerClientId = Guid.NewGuid();
        Log.Information("Generated a new local multiplayer client id for this instance.");
        return true;
    }

    internal static bool EnsureHttpCommandAccessToken(Settings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.HttpCommandAccessToken) &&
            settings.HttpCommandAccessToken.Length >= 32)
        {
            return false;
        }

        settings.HttpCommandAccessToken = Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32));
        Log.Information("Generated a new HTTP command access token for this instance.");
        return true;
    }

    public static void LoadSettings()
    {
        try
        {
            if (Storage.FileExists(GamePaths.SettingsFile))
            {
                using (var stream = Storage.OpenFile(GamePaths.SettingsFile, OpenFileMode.Read))
                {
                    var xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
                    AppConfigStore.ReadFromXml(xElement);
                    ConnectionDirectory.ReadFromXml(xElement);

                    foreach (var item in xElement.Elements())
                    {
                        var name = "<unknown>";
                        try
                        {
                            if (item.Name.LocalName == "Setting")
                            {
                                name = XmlUtils.GetAttributeValue<string>(item, "Name");
                                var attributeValue = XmlUtils.GetAttributeValue<string>(item, "Value");
                                var propertyInfo = (from pi in typeof(Settings).GetRuntimeProperties()
                                                    where pi.Name == name &&
                                                          pi.GetMethod != null &&
                                                          !pi.GetMethod.IsStatic && pi.GetMethod.IsPublic &&
                                                          pi.SetMethod != null &&
                                                          pi.SetMethod.IsPublic
                                                    select pi).FirstOrDefault();
                                if ((object?)propertyInfo != null)
                                {
                                    var value = HumanReadableConverter.ConvertFromString(propertyInfo.PropertyType,
                                        attributeValue);
                                    propertyInfo.SetValue(Current, value, null);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(string.Format("Setting \"{0}\" could not be loaded. Reason: {1}",
                                new object[]
                                {
                                    name,
                                    ex.Message
                                })
                            );
                        }
                    }
                }

                Log.Information("Loaded settings.");
            }
            else
            {
                SaveSettings();
            }
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
            var xElement = new XElement("Settings");

            foreach (var item in from pi in typeof(Settings).GetRuntimeProperties()
                                 where pi.GetMethod != null && !pi.GetMethod.IsStatic && pi.GetMethod.IsPublic &&
                                       pi.SetMethod != null && pi.SetMethod.IsPublic
                                 select pi)
            {
                try
                {
                    var value = HumanReadableConverter.ConvertToString(item.GetValue(Current, null) ?? string.Empty);
                    var node = XmlUtils.AddElement(xElement, "Setting");
                    XmlUtils.SetAttributeValue(node, "Name", item.Name);
                    XmlUtils.SetAttributeValue(node, "Value", value);
                }
                catch (Exception ex)
                {
                    Log.Warning(string.Format("Setting \"{0}\" could not be saved. Reason: {1}", new object[]
                    {
                        item.Name,
                        ex.Message
                    }));
                }
            }

            AppConfigStore.WriteToXml(xElement);
            ConnectionDirectory.WriteToXml(xElement);

            using (var stream = Storage.OpenFile(GamePaths.SettingsFile, OpenFileMode.Create))
            {
                XmlUtils.SaveXmlToStream(xElement, stream, null, true);
            }

            Log.Information("Saved settings");
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser("Saving settings failed.", e);
        }
    }
}
