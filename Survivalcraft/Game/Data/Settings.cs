using Engine.Graphics;

namespace Game;

public class Settings
{
    public int ServerPort { get; set; } = 28887;

    public int BroadcastPort { get; set; } = 28888;

    public int HttpCommandPort { get; set; } = Commands.HttpCommandProtocol.DefaultPort;

    public string HttpCommandAccessToken { get; set; } = string.Empty;

    public float SoundsVolume
    {
        get;
        set => field = MathUtils.Saturate(value);
    } = 0.5f;

    public float MusicVolume
    {
        get;
        set => field = MathUtils.Saturate(value);
    } = 0.5f;

    public int VisibilityRange { get; set; } = 128;

    public int MaxClientVisibilityRange { get; set; } =
        Network.NetworkTerrainPolicy.DefaultMaxClientVisibilityRange;

    public bool UseVr { get; set; }

    public float UIScale { get; set; } = 1f;

    public ResolutionMode ResolutionMode
    {
        get;
        set
        {
            if (Equals(value, field))
            {
                return;
            }

            field = value;
        }
    } = ResolutionMode.High;

    public float ViewAngle { get; set; } = 1f;

    public SkyRenderingMode SkyRenderingMode { get; set; } = SkyRenderingMode.Full;

    public bool TerrainMipmapsEnabled { get; set; } = true;

    public bool ObjectsShadowsEnabled { get; set; } = true;

    public float Brightness
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
            SettingsManager.NotifyBrightnessChanged();
        }
    } = 0.5f;

    public bool VSync { get; set; } = true;

    public bool ShowGuiInScreenshots { get; set; }

    public bool ShowLogoInScreenshots { get; set; } = true;

    public ScreenshotSize ScreenshotSize { get; set; } = ScreenshotSize.ScreenSize;

    public bool HideMoveLookPads { get; set; }

    public MoveControlMode MoveControlMode { get; set; } = MoveControlMode.Pad;

    public LookControlMode LookControlMode { get; set; } = LookControlMode.EntireScreen;

    public bool LeftHandedLayout { get; set; }

    public bool FlipVerticalAxis { get; set; }

    public float MoveSensitivity { get; set; } = 0.5f;

    public float LookSensitivity { get; set; } = 0.5f;

    public float GamepadDeadZone { get; set; } = 0.16f;

    public float GamepadCursorSpeed { get; set; } = 1f;

    public float CreativeDigTime { get; set; } = 0.2f;

    public float CreativeReach { get; set; } = 7.5f;

    public float MinimumHoldDuration { get; set; } = 0.5f;

    public float MinimumDragDistance { get; set; } = 10f;

    public bool AutoJump { get; set; } = true;

    public bool HorizontalCreativeFlight { get; set; } = true;

    public string DropboxAccessToken { get; set; } = string.Empty;

    public string MotdUpdateUrl { get; set; } = string.Empty;

    public string MotdUpdateCheckUrl { get; set; } = string.Empty;

    public double MotdUpdatePeriodHours { get; set; } = 12.0;

    public DateTime MotdLastUpdateTime { get; set; } = DateTime.MinValue;

    public string MotdLastDownloadedData { get; set; } = string.Empty;

    public string CommunityAccessUser { get; set; } = string.Empty;

    public string OnlineAccessToken { get; set; } = string.Empty;

    public string CommunityAccessToken { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public CommunityContentMode CommunityContentMode { get; set; } = CommunityContentMode.Normal;

    public bool UseReducedZRange
    {
        get;
        set
        {
            field = value;
            Display.UseReducedZRange = value;
        }
    }

    public int IsolatedStorageMigrationCounter { get; set; }

    public bool DisplayDebugInfo { get; set; }

    public bool DisplayFpsRibbon { get; set; }

    public bool ShowPlayerInformationOverlay { get; set; } = true;

    public PlayerListFilter PlayerInformationFilter { get; set; } =
        PlayerListFilter.All;

    public bool ShowMessageHistoryOverlay { get; set; } = true;

    public int NewYearCelebrationLastYear { get; set; } = 2015;

    public ScreenLayout ScreenLayout1 { get; set; } = ScreenLayout.Single;

    public ScreenLayout ScreenLayout2 { get; set; } = ScreenLayout.DoubleVertical;

    public ScreenLayout ScreenLayout3 { get; set; } = ScreenLayout.TripleVertical;

    public ScreenLayout ScreenLayout4 { get; set; } = ScreenLayout.Quadruple;

    public bool UpsideDownLayout { get; set; }

    public string BulletinTime { get; set; } = string.Empty;

    public string ScpboxUserInfo { get; set; } = string.Empty;

    public string CommunityNickName { get; set; } = string.Empty;

    /** 生物数量配置 **/
    public int CreatureTotalLimit { get; set; } = 24;

    public int CreatureAreaLimit { get; set; } = 3;

    public int CreatureMaxPlayerAreaLimit { get; set; } = 48;

    public int CreatureMaxPointLimit { get; set; } = 3;

    public int CreatureAreaRadius { get; set; } = 16;

    public int CreatureTotalLimitConstant { get; set; } = 18;

    public int CreatureAreaLimitConstant { get; set; } = 4;

    public int CreatureAreaRadiusConstant { get; set; } = 2;

    public float CreatureSpawnIntervalTime { get; set; } = 60f;

    public float CreatureConstantSpawnIntervalTime { get; set; } = 1f;

    public int ServerChunkCountSendPer { get; set; } =
        Network.NetworkTerrainPolicy.DefaultServerChunkCountSendPer;

    public bool AutoGarbageCollect { get; set; } = true;

    public string DefaultModRepositoryUrl { get; set; } = string.Empty;

    public int RejectedUpdateCount { get; set; } = 0;
}

public enum PlayerListFilter
{
    All,
    SameTeam
}
