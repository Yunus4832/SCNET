using System.Xml.Linq;

using Engine.Serialization;

using EntitySystem.XmlUtilities;

using Game.Network;

namespace Game.Managers;

public static class SettingsManager
{
    public static Settings Current { get; } = new();

    public static event Action? BrightnessChanged;

    public static void SetOnlineAccessToken(string newToken)
    {
        Current.OnlineAccessToken = newToken;
        SaveSettings();
    }

    internal static void NotifyBrightnessChanged()
    {
        BrightnessChanged?.Invoke();
    }

    public static void Initialize()
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            Current.CommunityContentMode = CommunityContentMode.Disabled;
        }
        else
        {
            Current.CommunityAccessToken = Guid.NewGuid().ToString();
        }

        var machineId = PlatformManager.Platform is Platform.Android
            ? GetMachineID.GetAndroidID()
            : GetMachineID.GetMachineGuid();

        Current.OnlineAccessToken = !string.IsNullOrEmpty(machineId)
            ? HashUtils.ComputeMd5(machineId)
            : Guid.NewGuid().ToString();

        if (PlatformManager.Platform is Platform.Android)
        {
            Current.UIScale = 0.8f;
        }

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
        VersionsManager.CompareVersions(Current.LastLaunchedVersion, "1.29");
        if (VersionsManager.CompareVersions(Current.LastLaunchedVersion, "2.1") < 0)
        {
            Current.MinimumDragDistance = 10f;
        }

        if (VersionsManager.CompareVersions(Current.LastLaunchedVersion, "2.2") < 0)
        {
            if (Utilities.GetTotalAvailableMemory() < 524288000)
            {
                Current.VisibilityRange = MathUtils.Min(64, Current.VisibilityRange);
            }
            else if (Utilities.GetTotalAvailableMemory() < 1048576000)
            {
                Current.VisibilityRange = MathUtils.Min(112, Current.VisibilityRange);
            }
        }

        if (VersionsManager.CompareVersions(Current.LastLaunchedVersion, "2.4") < 0)
        {
            Current.TerrainMipmapsEnabled = true;
        }

        Window.Deactivated += SaveSettings;
    }

    public static void LoadSettings()
    {
        try
        {
            ModSelectionSettings.ReplaceDisabledPackages([]);
            if (Storage.FileExists(GamePaths.SettingsFile))
            {
                using (var stream = Storage.OpenFile(GamePaths.SettingsFile, OpenFileMode.Read))
                {
                    var xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
                    var disabledPackageIds = new List<string>();
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
                            else if (item.Name.LocalName == "DisableMods")
                            {
                                foreach (var xElement1 in item.Elements())
                                {
                                    var packageId = xElement1.Attribute("PackageName")?.Value;
                                    if (!string.IsNullOrWhiteSpace(packageId))
                                    {
                                        disabledPackageIds.Add(packageId);
                                    }
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

                    ModSelectionSettings.ReplaceDisabledPackages(disabledPackageIds);
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
                    if (RunMode.Value is RunModeType.HeadlessServer)
                    {
                        if (item.Name == nameof(Settings.FullScreenMode))
                        {
                            continue;
                        }
                    }

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

            var xElement1 = new XElement("DisableMods");
            foreach (var packageName in ModSelectionSettings.DisabledPackages.OrderBy(name => name,
                         StringComparer.OrdinalIgnoreCase))
            {
                var element = new XElement("Mod");
                element.SetAttributeValue("PackageName", packageName);
                xElement1.Add(element);
            }

            xElement.Add(xElement1);
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
