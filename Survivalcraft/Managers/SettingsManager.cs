using System.Globalization;
using System.Xml.Linq;

using Engine.Serialization;

using EntitySystem.XmlUtilities;

using Game.ContentProviders;

namespace Game.Managers;

public static class SettingsManager
{
    // 添加字段用于保存冷却时间到配置文件
    private const string _lastAccessTokenChangeTimeKey = "LastAccessTokenChangeTime";

    // 添加字段记录上次修改时间
    private static DateTime _lastAccessTokenChangeTime = DateTime.MinValue;

    public static bool UsePrimaryMemoryBank { get; set; }

    public static bool AllowInitialIntro { get; set; }

    public static int ServerPort { get; set; }

    public static int BroadcastPort { get; set; }


    public static bool DeleteWorldNeedToText { get; set; }

    public static float SoundsVolume
    {
        get;
        set => field = MathUtils.Saturate(value);
    }

    public static float MusicVolume
    {
        get;
        set => field = MathUtils.Saturate(value);
    }

    public static int VisibilityRange { get; set; }

    public static bool UseVr { get; set; }

    public static float UIScale { get; set; }

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
    }

    public static float ViewAngle { get; set; }

    public static SkyRenderingMode SkyRenderingMode { get; set; }

    public static bool TerrainMipmapsEnabled { get; set; }

    public static bool ObjectsShadowsEnabled { get; set; }

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
    }

    public static bool VSync { get; set; } = true;

    public static bool ShowGuiInScreenshots { get; set; }

    public static bool ShowLogoInScreenshots { get; set; }

    public static ScreenshotSize ScreenshotSize { get; set; }

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

    public static MoveControlMode MoveControlMode { get; set; }

    public static LookControlMode LookControlMode { get; set; }

    public static bool LeftHandedLayout { get; set; }

    public static bool FlipVerticalAxis { get; set; }

    public static float MoveSensitivity { get; set; }

    public static float LookSensitivity { get; set; }

    public static float GamepadDeadZone { get; set; }

    public static float GamepadCursorSpeed { get; set; }

    public static float CreativeDigTime { get; set; }

    public static float CreativeReach { get; set; }

    public static float MinimumHoldDuration { get; set; }

    public static float MinimumDragDistance { get; set; }

    public static bool AutoJump { get; set; }

    public static bool HorizontalCreativeFlight { get; set; }

    public static string DropboxAccessToken { get; set; } = string.Empty;

    public static string MotdUpdateUrl { get; set; } = string.Empty;

    public static string MotdUpdateCheckUrl { get; set; } = string.Empty;

    public static string CommunityAccessUser { get; set; } = string.Empty;

    public static string OnlineAccessToken { get; set; } = string.Empty;

    public static string CommunityAccessToken { get; set; } = string.Empty;

    public static bool MotdUseBackupUrl { get; set; }

    public static double MotdUpdatePeriodHours { get; set; }

    public static DateTime MotdLastUpdateTime { get; set; }

    public static string MotdLastDownloadedData { get; set; } = string.Empty;

    public static string UserId { get; set; } = string.Empty;

    public static string LastLaunchedVersion { get; set; } = string.Empty;

    public static CommunityContentMode CommunityContentMode { get; set; }

    public static bool MultithreadedTerrainUpdate { get; set; }

    public static bool UseReducedZRange { get; set; }

    public static bool EnableMod { get; set; }

    public static int IsolatedStorageMigrationCounter { get; set; }

    public static bool DisplayFpsCounter { get; set; }

    public static bool DisplayFpsRibbon { get; set; }

    public static int NewYearCelebrationLastYear { get; set; }

    public static ScreenLayout ScreenLayout1 { get; set; }

    public static ScreenLayout ScreenLayout2 { get; set; }

    public static ScreenLayout ScreenLayout3 { get; set; }

    public static ScreenLayout ScreenLayout4 { get; set; }

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
    public static int CreatureTotalLimit { get; set; }

    public static int CreatureAreaLimit { get; set; }

    public static int CreatureMaxPlayerAreaLimit { get; set; }

    public static int CreatureMaxPointLimit { get; set; }

    public static int CreatureAreaRadius { get; set; }

    public static int CreatureTotalLimitConstant { get; set; }

    public static int CreatureAreaLimitConstant { get; set; }

    public static int CreatureAreaRadiusConstant { get; set; }

    public static float CreatureSpawnIntervalTime { get; set; }

    public static float CreatureConstantSpawnIntervalTime { get; set; }

    public static int ServerChunkCountSendPer { get; set; }

    public static bool AutoGarbageCollect { get; set; }

    /// <summary>
    /// 告示牌通电是否广播所有玩家
    /// </summary>
    public static bool GlobalSignBlockAlert { get; set; }

    public static string LiteNetLibLogLevel { get; set; } = string.Empty;

    public static string WillEnterServer { get; set; } = string.Empty;

    public static string WillEnterServerPwd { get; set; } = string.Empty;

    public static bool StartModServer { get; set; }

    public static string ModServerAddress { get; set; } = string.Empty;

    public static int RejectedUpdateCount { get; set; }

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
        LiteNetLibLogLevel = "Error"; //Warning,Error,Trace,Info
        ServerChunkCountSendPer = 100;
        CreatureTotalLimit = 24;
        CreatureAreaLimit = 3;
        CreatureMaxPlayerAreaLimit = 48;
        CreatureMaxPointLimit = 3;
        CreatureAreaRadius = 16;
        CreatureTotalLimitConstant = 18;
        CreatureAreaLimitConstant = 4;
        CreatureAreaRadiusConstant = 2;
        CreatureSpawnIntervalTime = 60f;
        CreatureConstantSpawnIntervalTime = 1f;
#if SERVER
        CommunityAccessToken = string.Empty;
#else
        CommunityAccessToken = Guid.NewGuid().ToString();
#endif
        ServerPort = 28887;
        BroadcastPort = 28888;
        DisplayLog = false;
        EnableMod = true;
        AutoGarbageCollect = true;
        GlobalSignBlockAlert = true;
        VisibilityRange = 128;
        ResolutionMode = ResolutionMode.High;
        ViewAngle = 1f;
        TerrainMipmapsEnabled = true;
        SkyRenderingMode = SkyRenderingMode.Full;
        ObjectsShadowsEnabled = true;
        SoundsVolume = 0.5f;
        MusicVolume = 0.5f;
        Brightness = 0.5f;
        VSync = true;
        ShowGuiInScreenshots = false;
        ShowLogoInScreenshots = true;
        ScreenshotSize = ScreenshotSize.ScreenSize;
        MoveControlMode = MoveControlMode.Pad;
        HideMoveLookPads = false;
        AllowInitialIntro = true;
        DeleteWorldNeedToText = false;
        BlocksTextureFileName = string.Empty;
        LookControlMode = LookControlMode.EntireScreen;
        FlipVerticalAxis = false;
#if ANDROID
        UIScale = 1f;
#endif
#if DESKTOP
        UIScale = 0.8f;
#endif
#if ANDROID
        OnlineAccessToken = !string.IsNullOrEmpty(GetMachineID.GetAndroidID())
            ? ModsManager.GetMd5(GetMachineID.GetAndroidID())
            : Guid.NewGuid().ToString();
#endif
#if DESKTOP
        OnlineAccessToken = !string.IsNullOrEmpty(GetMachineID.GetMachineGuid())
            ? ModsManager.GetMd5(GetMachineID.GetMachineGuid())
            : Guid.NewGuid().ToString();
#endif
        MoveSensitivity = 0.5f;
        LookSensitivity = 0.5f;
        GamepadDeadZone = 0.16f;
        GamepadCursorSpeed = 1f;
        CreativeDigTime = 0.2f;
        CreativeReach = 7.5f;
        MinimumHoldDuration = 0.5f;
        MinimumDragDistance = 10f;
        AutoJump = true;
        HorizontalCreativeFlight = false;
        DropboxAccessToken = string.Empty;
        CommunityAccessUser = string.Empty;
        MotdUpdateUrl = SchubExternalContentProvider.GetPath("/com/motd?v={0}&l={1}");
        MotdUpdateCheckUrl =
            SchubExternalContentProvider.GetPath("/com/motd?v={0}&cmd=version_check&platform={1}&apiv={2}&l={3}");
        MotdUpdatePeriodHours = 12.0;
        MotdLastUpdateTime = DateTime.MinValue;
        MotdLastDownloadedData = string.Empty;
        UserId = string.Empty;
        LastLaunchedVersion = string.Empty;
#if SERVER
        CommunityContentMode = CommunityContentMode.Disabled;
#else
        CommunityContentMode = CommunityContentMode.Normal;
#endif
        MultithreadedTerrainUpdate = true;
        NewYearCelebrationLastYear = 2015;
        ScreenLayout1 = ScreenLayout.Single;
        ScreenLayout2 = Window.ScreenSize.X / (float)Window.ScreenSize.Y > 1.33333337f
            ? ScreenLayout.DoubleVertical
            : ScreenLayout.DoubleHorizontal;
        ScreenLayout3 = Window.ScreenSize.X / (float)Window.ScreenSize.Y > 1.33333337f
            ? ScreenLayout.TripleVertical
            : ScreenLayout.TripleHorizontal;
        ScreenLayout4 = ScreenLayout.Quadruple;
        BulletinTime = string.Empty;
        ScpboxUserInfo = string.Empty;
        CommunityNickName = string.Empty;
        HorizontalCreativeFlight = true;
        WillEnterServer = string.Empty;
        WillEnterServerPwd = string.Empty;
        StartModServer = true;
        ModServerAddress = "";
        RejectedUpdateCount = 0;

        if (!Storage.DirectoryExists(ModsManager.ConfigPath))
        {
            Storage.CreateDirectory(ModsManager.ConfigPath);
        }

        LoadSettings();
        VersionsManager.CompareVersions(LastLaunchedVersion, "1.29");
        _ = 0;
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
            if (Storage.FileExists(ModsManager.SettingPath))
            {
                using (var stream = Storage.OpenFile(ModsManager.SettingPath, OpenFileMode.Read))
                {
                    var xElement = XmlUtils.LoadXmlFromStream(stream, null, true);
                    ModsManager.LoadSettings(xElement);

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
                                    var modInfo = new ModInfo
                                    {
                                        PackageName = xElement1.Attribute("PackageName")?.Value ?? string.Empty,
                                        Version = xElement1.Attribute("Version")?.Value ?? string.Empty,
                                    };
                                    ModsManager.DisabledMods.Add(modInfo);
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
#if SERVER
                    if (item.Name == nameof(FullScreenMode))
                    {
                        continue;
                    }
#endif
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
            var xElement2 = new XElement("ModSettings");
            foreach (var modEntity in ModsManager.ModListAll)
            {
                if (ModsManager.DisabledMods.Contains(modEntity.ModInfo))
                {
                    var element = new XElement("Mod");
                    element.SetAttributeValue("PackageName", modEntity.ModInfo.PackageName);
                    element.SetAttributeValue("Version", modEntity.ModInfo.Version);
                    xElement1.Add(element);
                }
            }

            xElement.Add(xElement1);
            ModsManager.SaveSettings(xElement);
            ModsManager.SaveModSettings(xElement2);

            using (var stream = Storage.OpenFile(ModsManager.SettingPath, OpenFileMode.Create))
            {
                XmlUtils.SaveXmlToStream(xElement, stream, null, true);
            }

            using (var stream = Storage.OpenFile(ModsManager.ModsSettingPath, OpenFileMode.Create))
            {
                XmlUtils.SaveXmlToStream(xElement2, stream, null, true);
            }

            Log.Information("Saved settings");
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser("Saving settings failed.", e);
        }
    }
}
