using System.Xml.Linq;

using Game.Network;
using Game.Network.Enums;

namespace Game.Screens;

public class SettingsPerformanceScreen : Screen
{
    private static readonly List<int> _visibilityRanges =
    [
        32, 48, 64, 80, 96, 112,
        128, 160, 192, 224, 256,
        320, 384, 448, 512, 576,
        640, 704, 768, 832, 896,
        960, 1024
    ];

    private const string _typeName = "SettingsPerformanceScreen";

    private readonly ButtonWidget _displayFpsCounterButton;

    private readonly ButtonWidget _displayFpsRibbonButton;

    private int _enterVisibilityRange;

    private readonly ButtonWidget _framerateLimitButton;

    private readonly ButtonWidget _objectShadowsButton;

    private readonly ButtonWidget _resolutionButton;

    private readonly ButtonWidget _skyRenderingModeButton;

    private readonly ButtonWidget _terrainMipmapsButton;

    private readonly SliderWidget _visibilityRangeSlider;

    private readonly LabelWidget _visibilityRangeWarningLabel;

    public SettingsPerformanceScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/SettingsPerformanceScreen");
        LoadContents(this, node);
        _resolutionButton = Children.Find<ButtonWidget>("ResolutionButton")!;
        _visibilityRangeSlider = Children.Find<SliderWidget>("VisibilityRangeSlider")!;
        _visibilityRangeWarningLabel = Children.Find<LabelWidget>("VisibilityRangeWarningLabel")!;
        _terrainMipmapsButton = Children.Find<ButtonWidget>("TerrainMipmapsButton")!;
        _skyRenderingModeButton = Children.Find<ButtonWidget>("SkyRenderingModeButton")!;
        _objectShadowsButton = Children.Find<ButtonWidget>("ObjectShadowsButton")!;
        _framerateLimitButton = Children.Find<ButtonWidget>("FramerateLimitButton")!;
        _displayFpsCounterButton = Children.Find<ButtonWidget>("DisplayFpsCounterButton")!;
        _displayFpsRibbonButton = Children.Find<ButtonWidget>("DisplayFpsRibbonButton")!;
        _visibilityRangeSlider.MinValue = 0f;
        _visibilityRangeSlider.MaxValue = _visibilityRanges.Count - 1;
    }

    public override void Enter(object[] parameters)
    {
        _enterVisibilityRange = SettingsManager.VisibilityRange;
        if (CommonLib.WorkType == WorkType.Client)
        {
            _visibilityRangeSlider.MaxValue = 10;
        }
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (_resolutionButton.IsClicked)
        {
            var enumValues = EnumUtils.GetEnumValues(typeof(ResolutionMode));
            SettingsManager.ResolutionMode =
                (ResolutionMode)((enumValues.IndexOf((int)SettingsManager.ResolutionMode) + 1) % enumValues.Count);
        }

        if (_visibilityRangeSlider.IsSliding)
        {
            SettingsManager.VisibilityRange =
                _visibilityRanges
                    [MathUtils.Clamp((int)_visibilityRangeSlider.Value, 0, _visibilityRanges.Count - 1)];
        }

        if (_terrainMipmapsButton.IsClicked)
        {
            SettingsManager.TerrainMipmapsEnabled = !SettingsManager.TerrainMipmapsEnabled;
        }

        if (_skyRenderingModeButton.IsClicked)
        {
            var enumValues3 = EnumUtils.GetEnumValues(typeof(SkyRenderingMode));
            SettingsManager.SkyRenderingMode =
                (SkyRenderingMode)((enumValues3.IndexOf((int)SettingsManager.SkyRenderingMode) + 1) %
                                   enumValues3.Count);
        }

        if (_objectShadowsButton.IsClicked)
        {
            SettingsManager.ObjectsShadowsEnabled = !SettingsManager.ObjectsShadowsEnabled;
        }

        if (_framerateLimitButton.IsClicked)
        {
            SettingsManager.VSync = !SettingsManager.VSync;
        }

        if (_displayFpsCounterButton.IsClicked)
        {
            SettingsManager.DisplayFpsCounter = !SettingsManager.DisplayFpsCounter;
        }

        if (_displayFpsRibbonButton.IsClicked)
        {
            SettingsManager.DisplayFpsRibbon = !SettingsManager.DisplayFpsRibbon;
        }

        _resolutionButton.Text = LanguageManager.Get("ResolutionMode", SettingsManager.ResolutionMode.ToString());
        _visibilityRangeSlider.Value = _visibilityRanges.IndexOf(SettingsManager.VisibilityRange) >= 0
            ? _visibilityRanges.IndexOf(SettingsManager.VisibilityRange)
            : 64;
        _visibilityRangeSlider.Text = string.Format(LanguageManager.Get(_typeName, 1), SettingsManager.VisibilityRange);
        if (SettingsManager.VisibilityRange <= 48)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 2);
        }
        else if (SettingsManager.VisibilityRange <= 64)
        {
            _visibilityRangeWarningLabel.IsVisible = false;
        }
        else if (SettingsManager.VisibilityRange <= 112)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 3);
        }
        else if (SettingsManager.VisibilityRange <= 224)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 4);
        }
        else if (SettingsManager.VisibilityRange <= 384)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 5);
        }
        else if (SettingsManager.VisibilityRange <= 512)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 6);
        }
        else
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 7);
        }

        _terrainMipmapsButton.Text =
            SettingsManager.TerrainMipmapsEnabled ? LanguageManager.Enable : LanguageManager.Disable;

        _skyRenderingModeButton.Text =
            LanguageManager.Get("SkyRenderingMode", SettingsManager.SkyRenderingMode.ToString());

        _objectShadowsButton.Text =
            SettingsManager.ObjectsShadowsEnabled ? LanguageManager.Enable : LanguageManager.Disable;

        _framerateLimitButton.Text = SettingsManager.VSync
            ? LanguageManager.Get(_typeName, 11)
            : LanguageManager.Get(_typeName, 8);

        _displayFpsCounterButton.Text = SettingsManager.DisplayFpsCounter ? LanguageManager.Yes : LanguageManager.No;

        _displayFpsRibbonButton.Text = SettingsManager.DisplayFpsRibbon ? LanguageManager.Yes : LanguageManager.No;

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            var flag = SettingsManager.VisibilityRange > 128;
            if (SettingsManager.VisibilityRange > _enterVisibilityRange && flag)
            {
                DialogsManager.ShowDialog(null, new MessageDialog(LanguageManager.Get(_typeName, 9),
                    LanguageManager.Get(_typeName, 10), LanguageManager.Ok, LanguageManager.Back,
                    delegate(MessageDialogButton button)
                    {
                        if (button == MessageDialogButton.Button1)
                        {
                            SettingsManager.SaveSettings();
                            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
                        }
                    }));
            }
            else
            {
                SettingsManager.SaveSettings();
                ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
            }
        }
    }
}
