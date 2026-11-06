using Engine.Graphics;

namespace Game.Configuration;

public sealed class ClientPreferences
{
    public float SoundsVolume { get; set => field = MathUtils.Saturate(value); } = 0.5f;

    public float MusicVolume { get; set => field = MathUtils.Saturate(value); } = 0.5f;

    public int VisibilityRange { get; set; } = 128;

    public bool UseVr { get; set; }

    public float UIScale { get; set; } = 1f;

    public ResolutionMode ResolutionMode { get; set; } = ResolutionMode.High;

    public float ViewAngle { get; set; } = 1f;

    public SkyRenderingMode SkyRenderingMode { get; set; } = SkyRenderingMode.Full;

    public bool TerrainMipmapsEnabled { get; set; } = true;

    public bool ObjectsShadowsEnabled { get; set; } = true;

    public float Brightness { get; set => field = MathUtils.Clamp(value, 0f, 1f); } = 0.5f;

    public bool VSync { get; set; } = true;

    public bool ShowGuiInScreenshots { get; set; }

    public bool ShowLogoInScreenshots { get; set; } = true;

    public ScreenshotSize ScreenshotSize { get; set; } = ScreenshotSize.ScreenSize;

    public MoveControlMode MoveControlMode { get; set; } = MoveControlMode.Pad;

    public LookControlMode LookControlMode { get; set; } = LookControlMode.EntireScreen;

    public bool LeftHandedLayout { get; set; }

    public bool HudSafeAreaEnabled { get; set; }

    public bool HudSafeAreaCustomPaddingEnabled { get; set; }

    public float HudSafeAreaPadding { get; set => field = MathUtils.Clamp(value, 0f, 80f); } = 32f;

    public bool FlipVerticalAxis { get; set; }

    public float MoveSensitivity { get; set; } = 0.5f;

    public float LookSensitivity { get; set; } = 0.5f;

    public float GamepadDeadZone { get; set; } = 0.16f;

    public float GamepadCursorSpeed { get; set; } = 1f;

    public float MinimumHoldDuration { get; set; } = 0.5f;

    public float MinimumDragDistance { get; set; } = 10f;

    public bool AutoJump { get; set; } = true;

    public bool UseReducedZRange
    {
        get;
        set
        {
            field = value;
            Display.UseReducedZRange = value;
        }
    }

    public bool DisplayDebugInfo { get; set; }

    public bool DisplayFpsRibbon { get; set; }

    public bool ShowCharacterInformationOverlay { get; set; } = true;

    public CharacterListFilter CharacterInformationFilter { get; set; } =
        CharacterListFilter.All;

    public bool ShowMessageHistoryOverlay { get; set; } = true;

    public bool UpsideDownLayout { get; set; }
}
