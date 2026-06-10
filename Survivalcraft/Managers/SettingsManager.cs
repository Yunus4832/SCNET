using System.Globalization;
using System.Xml.Linq;

using Engine.Serialization;

using EntitySystem.XmlUtilities;

using Game.ContentProviders;
using Game.Modding;
using Game.Network;

namespace Game.Managers;

public static class SettingsManager
{
    // 添加字段用于保存冷却时间到配置文件
    private const string _lastAccessTokenChangeTimeKey = "LastAccessTokenChangeTime";

    // 添加字段记录上次修改时间
    private static DateTime _lastAccessTokenChangeTime = DateTime.MinValue;

    public static bool UsePrimaryMemoryBank { get; set; }

    public static bool AllowInitialIntro { get; set; } = true;

    public static int ServerPort { get; set; } = 28887;

    public static int BroadcastPort { get; set; } = 28888;


    public static bool DeleteWorldNeedToText { get; set; }

    public static float SoundsVolume
    {
        get;
        set => field = MathUtils.Saturate(value);
    } = 0.5f;

    public static float MusicVolume
    {
        get;
        set => field = MathUtils.Saturate(value);
    } = 0.5f;

    public static int VisibilityRange { get; set; } = 128;

    public static bool UseVr { get; set; }

    public static float UIScale { get; set; } = 1f;

    public static ResolutionMode ResolutionMode
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            field = value;
            SettingChanged?.Invoke("ResolutionMode");
        }
    } = ResolutionMode.High;

    public static float ViewAngle { get; set; } = 1f;

    public static SkyRenderingMode SkyRenderingMode { get; set; } = SkyRenderingMode.Full;

    public static bool TerrainMipmapsEnabled { get; set; } = true;

    public static bool ObjectsShadowsEnabled { get; set; } = true;

    public static float Brightness
    {
        get;
        set
        {
            value = MathUtils.Clamp(value, 0f, 1f);
            if (value.CloseTo(field))
            {
                return;
            }

            field = value;
            SettingChanged?.Invoke("Brightness");
        }
    } = 0.5f;

    public static bool VSync { get; set; } = true;

    public static bool ShowGuiInScreenshots { get; set; }

    public static bool ShowLogoInScreenshots { get; set; } = true;

    public static ScreenshotSize ScreenshotSize { get; set; } = ScreenshotSize.ScreenSize;

    public static WindowMode WindowMode
    {
        get;
        set
        {
            if (value == field)
            {
                return;
            }

            Window.WindowMode = value;
            field = value;
        }
    }

    public static GuiSize GuiSize { get; set; }

    public static bool HideMoveLookPads { get; set; }

    public static string BlocksTextureFileName { get; set; } = string.Empty;

    public static MoveControlMode MoveControlMode { get; set; } = MoveControlMode.Pad;

    public static LookControlMode LookControlMode { get; set; } = LookControlMode.EntireScreen;

    public static bool LeftHandedLayout { get; set; }

    public static bool FlipVerticalAxis { get; set; }

    public static float MoveSensitivity { get; set; } = 0.5f;

    public static float LookSensitivity { get; set; } = 0.5f;

    public static float GamepadDeadZone { get; set; } = 0.16f;

    public static float GamepadCursorSpeed { get; set; } = 1f;

    public static float CreativeDigTime { get; set; } = 0.2f;

    public static float CreativeReach { get; set; } = 7.5f;

    public static float MinimumHoldDuration { get; set; } = 0.5f;

    public static float MinimumDragDistance { get; set; } = 10f;

    public static bool AutoJump { get; set; } = true;

    public static bool HorizontalCreativeFlight { get; set; } = true;

    public static string DropboxAccessToken { get; set; } = string.Empty;

    public static string MotdUpdateUrl { get; set; } = SchubExternalContentProvider.GetPath("/com/motd?v={0}&l={1}");

    public static string MotdUpdateCheckUrl { get; set; } =
        SchubExternalContentProvider.GetPath("/com/motd?v={0}&cmd=version_check&platform={1}&apiv={2}&l={3}");

    public static string CommunityAccessUser { get; set; } = string.Empty;

    public static string OnlineAccessToken { get; set; } = string.Empty;

    public static string CommunityAccessToken { get; set; } = string.Empty;

    public static bool MotdUseBackupUrl { get; set; }

    public static double MotdUpdatePeriodHours { get; set; } = 12.0;

    public static DateTime MotdLastUpdateTime { get; set; } = DateTime.MinValue;

    public static string MotdLastDownloadedData { get; set; } = string.Empty;

    public static string UserId { get; set; } = string.Empty;

    public static string LastLaunchedVersion { get; set; } = string.Empty;

    public static CommunityContentMode CommunityContentMode { get; set; } = CommunityContentMode.Normal;

    public static bool MultithreadedTerrainUpdate { get; set; } = true;

    public static bool UseReducedZRange { get; set; }

    public static bool EnableMod { get; set; } = true;

    public static int IsolatedStorageMigrationCounter { get; set; }

    public static bool DisplayFpsCounter { get; set; }

    public static bool DisplayFpsRibbon { get; set; }

    public static int NewYearCelebrationLastYear { get; set; } = 2015;

    public static ScreenLayout ScreenLayout1 { get; set; } = ScreenLayout.Single;

    public static ScreenLayout ScreenLayout2 { get; set; } = ScreenLayout.DoubleVertical;

    public static ScreenLayout ScreenLayout3 { get; set; } = ScreenLayout.TripleVertical;

    public static ScreenLayout ScreenLayout4 { get; set; } = ScreenLayout.Quadruple;

    public static bool UpsideDownLayout { get; set; }

    public static bool FullScreenMode
    {
        get => Window.WindowMode == WindowMode.Fullscreen;
        set => Window.WindowMode = value ? WindowMode.Fullscreen : WindowMode.Resizable;
    }

    public static bool DisplayLog { get; set; }

    public static string BulletinTime { get; set; } = string.Empty;

    public static string ScpboxUserInfo { get; set; } = string.Empty;

    public static string CommunityNickName { get; set; } = string.Empty;

    /** 生物数量配置 **/
    public static int CreatureTotalLimit { get; set; } = 24;

    public static int CreatureAreaLimit { get; set; } = 3;

    public static int CreatureMaxPlayerAreaLimit { get; set; } = 48;

    public static int CreatureMaxPointLimit { get; set; } = 3;

    public static int CreatureAreaRadius { get; set; } = 16;

    public static int CreatureTotalLimitConstant { get; set; } = 18;

    public static int CreatureAreaLimitConstant { get; set; } = 4;

    public static int CreatureAreaRadiusConstant { get; set; } = 2;

    public static float CreatureSpawnIntervalTime { get; set; } = 60f;

    public static float CreatureConstantSpawnIntervalTime { get; set; } = 1f;

    public static int ServerChunkCountSendPer { get; set; } = 100;

    public static bool AutoGarbageCollect { get; set; } = true;

    /// <summary>
    /// 告示牌通电是否广播所有玩家
    /// </summary>
    public static bool GlobalSignBlockAlert { get; set; } = true;

    /// <summary>
    /// Warning,Error,Trace,Info
    /// </summary>
    public static string LiteNetLibLogLevel { get; set; } = "Error";

    public static bool StartModServer { get; set; } = true;

    public static string ModServerAddress { get; set; } = string.Empty;

    public static int RejectedUpdateCount { get; set; } = 0;

    // 添加一个方法来设置 OnlineAccessToken，包含冷却时间检查
    public static void SetOnlineAccessToken(string newToken)
    {
        if (_lastAccessTokenChangeTime == DateTime.MinValue ||
            (DateTime.Now - _lastAccessTokenChangeTime).TotalMinutes >= 10)
        {
            OnlineAccessToken = newToken;
            _lastAccessTokenChangeTime = DateTime.Now; // 更新上次修改时间
            SaveSettings(); // 保存设置到文件
        }
        else
        {
            throw new InvalidOperationException("您需要等待10分钟后才能再次修改ID。");
        }
    }

    public static event Action<string>? SettingChanged;

    public static void Initialize()
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            CommunityContentMode = CommunityContentMode.Disabled;
        }
        else
        {
            CommunityAccessToken = Guid.NewGuid().ToString();
        }

#if ANDROID
        OnlineAccessToken = !string.IsNullOrEmpty(GetMachineID.GetAndroidID())
            ? HashUtils.ComputeMd5(GetMachineID.GetAndroidID())
            : Guid.NewGuid().ToString();
        UIScale = 0.8f;
#endif
#if DESKTOP
        OnlineAccessToken = !string.IsNullOrEmpty(GetMachineID.GetMachineGuid())
            ? HashUtils.ComputeMd5(GetMachineID.GetMachineGuid())
            : Guid.NewGuid().ToString();
#endif
        var screenWidth = RunMode.Value is RunModeType.HeadlessServer ? 1280 : Window.ScreenSize.X;
        var screenHeight = RunMode.Value is RunModeType.HeadlessServer ? 720 : Window.ScreenSize.Y;
        var isWideScreen = screenWidth / (float)screenHeight > 1.33333337f;
        ScreenLayout2 = isWideScreen ? ScreenLayout.DoubleVertical : ScreenLayout.DoubleHorizontal;
        ScreenLayout3 = isWideScreen ? ScreenLayout.TripleVertical : ScreenLayout.TripleHorizontal;

        if (!Storage.DirectoryExists(GamePaths.Config))
        {
            Storage.CreateDirectory(GamePaths.Config);
        }

        LoadSettings();
        VersionsManager.CompareVersions(LastLaunchedVersion, "1.29");
        if (VersionsManager.CompareVersions(LastLaunchedVersion, "2.1") < 0)
        {
            MinimumDragDistance = 10f;
        }

        if (VersionsManager.CompareVersions(LastLaunchedVersion, "2.2") < 0)
        {
            if (Utilities.GetTotalAvailableMemory() < 524288000)
            {
                VisibilityRange = MathUtils.Min(64, VisibilityRange);
            }
            else if (Utilities.GetTotalAvailableMemory() < 1048576000)
            {
                VisibilityRange = MathUtils.Min(112, VisibilityRange);
            }
        }

        if (VersionsManager.CompareVersions(LastLaunchedVersion, "2.4") < 0)
        {
            TerrainMipmapsEnabled = true;
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

                    // 加载 LastAccessTokenChangeTime
                    var lastChangeTimeElement = xElement.Element(_lastAccessTokenChangeTimeKey);
                    if (lastChangeTimeElement != null)
                    {
                        if (DateTime.TryParse(lastChangeTimeElement.Value, out var lastChangeTime))
                        {
                            _lastAccessTokenChangeTime = lastChangeTime;
                        }
                    }

                    foreach (var item in xElement.Elements())
                    {
                        var name = "<unknown>";
                        try
                        {
                            if (item.Name.LocalName == "Setting")
                            {
                                name = XmlUtils.GetAttributeValue<string>(item, "Name");
                                var attributeValue = XmlUtils.GetAttributeValue<string>(item, "Value");
                                var propertyInfo = (from pi in typeof(SettingsManager).GetRuntimeProperties()
                                    where pi.Name == name &&
                                          pi.GetMethod != null &&
                                          pi.GetMethod.IsStatic && pi.GetMethod.IsPublic &&
                                          pi.SetMethod != null &&
                                          pi.SetMethod.IsPublic
                                    select pi).FirstOrDefault();
                                if ((object?)propertyInfo != null)
                                {
                                    var value = HumanReadableConverter.ConvertFromString(propertyInfo.PropertyType,
                                        attributeValue);
                                    propertyInfo.SetValue(null, value, null);
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
                            Log.Warning(string.Format("Setting \"{0}\" could not be loaded. Reason: {1}", new object[2]
                            {
                                name,
                                ex.Message
                            }));
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

            // 保存 LastAccessTokenChangeTime
            XmlUtils.AddElement(xElement, _lastAccessTokenChangeTimeKey).Value =
                _lastAccessTokenChangeTime.ToString(CultureInfo.InvariantCulture);

            foreach (var item in from pi in typeof(SettingsManager).GetRuntimeProperties()
                     where pi.GetMethod != null && pi.GetMethod.IsStatic && pi.GetMethod.IsPublic &&
                           pi.SetMethod != null && pi.SetMethod.IsPublic
                     select pi)
            {
                try
                {
                    if (RunMode.Value is RunModeType.HeadlessServer)
                    {
                        if (item.Name == nameof(FullScreenMode))
                        {
                            continue;
                        }
                    }

                    var value = HumanReadableConverter.ConvertToString(item.GetValue(null, null) ?? string.Empty);
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
