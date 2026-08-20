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

    private const string _typeName = nameof(SettingsPerformanceScreen);

    private readonly ButtonWidget _displayDebugInfoButton;

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
        _displayDebugInfoButton = Children.Find<ButtonWidget>("DisplayDebugInfoButton")!;
        _displayFpsRibbonButton = Children.Find<ButtonWidget>("DisplayFpsRibbonButton")!;
        _visibilityRangeSlider.MinValue = 0f;
        _visibilityRangeSlider.MaxValue = _visibilityRanges.Count - 1;
    }

    public override void Enter(object[] parameters)
    {
        _enterVisibilityRange = SettingsManager.Current.VisibilityRange;
        if (CommonLib.WorkType == WorkType.Client)
        {
            _visibilityRangeSlider.MaxValue = _visibilityRanges.FindLastIndex(range =>
                range <= NetworkTerrainPolicy.DefaultMaxClientVisibilityRange);
        }
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (_resolutionButton.IsClicked)
        {
            var enumValues = EnumUtils.GetEnumValues(typeof(ResolutionMode));
            SettingsManager.Current.ResolutionMode =
                (ResolutionMode)((enumValues.IndexOf((int)SettingsManager.Current.ResolutionMode) + 1) %
                                 enumValues.Count);
        }

        if (_visibilityRangeSlider.IsSliding)
        {
            SettingsManager.Current.VisibilityRange =
                _visibilityRanges
                    [MathUtils.Clamp((int)_visibilityRangeSlider.Value, 0, _visibilityRanges.Count - 1)];
        }

        if (_terrainMipmapsButton.IsClicked)
        {
            SettingsManager.Current.TerrainMipmapsEnabled = !SettingsManager.Current.TerrainMipmapsEnabled;
        }

        if (_skyRenderingModeButton.IsClicked)
        {
            var enumValues3 = EnumUtils.GetEnumValues(typeof(SkyRenderingMode));
            SettingsManager.Current.SkyRenderingMode =
                (SkyRenderingMode)((enumValues3.IndexOf((int)SettingsManager.Current.SkyRenderingMode) + 1) %
                                   enumValues3.Count);
        }

        if (_objectShadowsButton.IsClicked)
        {
            SettingsManager.Current.ObjectsShadowsEnabled = !SettingsManager.Current.ObjectsShadowsEnabled;
        }

        if (_framerateLimitButton.IsClicked)
        {
            SettingsManager.Current.VSync = !SettingsManager.Current.VSync;
        }

        if (_displayDebugInfoButton.IsClicked)
        {
            SettingsManager.Current.DisplayDebugInfo = !SettingsManager.Current.DisplayDebugInfo;
        }

        if (_displayFpsRibbonButton.IsClicked)
        {
            SettingsManager.Current.DisplayFpsRibbon = !SettingsManager.Current.DisplayFpsRibbon;
        }

        _resolutionButton.Text =
            LanguageManager.Get("ResolutionMode", SettingsManager.Current.ResolutionMode.ToString());
        _visibilityRangeSlider.Value = _visibilityRanges.IndexOf(SettingsManager.Current.VisibilityRange) >= 0
            ? _visibilityRanges.IndexOf(SettingsManager.Current.VisibilityRange)
            : 64;
        _visibilityRangeSlider.Text =
            string.Format(LanguageManager.Get(_typeName, 1), SettingsManager.Current.VisibilityRange);
        if (SettingsManager.Current.VisibilityRange <= 48)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 2);
        }
        else if (SettingsManager.Current.VisibilityRange <= 64)
        {
            _visibilityRangeWarningLabel.IsVisible = false;
        }
        else if (SettingsManager.Current.VisibilityRange <= 112)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 3);
        }
        else if (SettingsManager.Current.VisibilityRange <= 224)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 4);
        }
        else if (SettingsManager.Current.VisibilityRange <= 384)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 5);
        }
        else if (SettingsManager.Current.VisibilityRange <= 512)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 6);
        }
        else
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = LanguageManager.Get(_typeName, 7);
        }

        _terrainMipmapsButton.Text = SettingsManager.Current.TerrainMipmapsEnabled
            ? LanguageManager.Enable
            : LanguageManager.Disable;

        _skyRenderingModeButton.Text = LanguageManager.Get(
            "SkyRenderingMode",
            SettingsManager.Current.SkyRenderingMode.ToString()
        );

        _objectShadowsButton.Text = SettingsManager.Current.ObjectsShadowsEnabled
            ? LanguageManager.Enable
            : LanguageManager.Disable;

        _framerateLimitButton.Text = SettingsManager.Current.VSync
            ? LanguageManager.Get(_typeName, 11)
            : LanguageManager.Get(_typeName, 8);

        _displayDebugInfoButton.Text = SettingsManager.Current.DisplayDebugInfo
            ? LanguageManager.Yes
            : LanguageManager.No;

        _displayFpsRibbonButton.Text = SettingsManager.Current.DisplayFpsRibbon
            ? LanguageManager.Yes
            : LanguageManager.No;

        if (Input is { Back: false, Cancel: false } && !Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            return;
        }

        var flag = SettingsManager.Current.VisibilityRange > 128;
        if (SettingsManager.Current.VisibilityRange > _enterVisibilityRange && flag)
        {
            DialogsManager.ShowDialog(null, new MessageDialog(LanguageManager.Get(_typeName, 9),
                LanguageManager.Get(_typeName, 10), LanguageManager.Ok, LanguageManager.Back,
                delegate(MessageDialogButton button)
                {
                    if (button != MessageDialogButton.Button1)
                    {
                        return;
                    }

                    SettingsManager.SaveSettings();
                    ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
                })
            );
        }
        else
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
