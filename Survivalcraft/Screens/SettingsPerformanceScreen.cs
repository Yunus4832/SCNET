using System.Xml.Linq;

using Game.Network.Chunks;

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
        _visibilityRangeSlider.MinValue = 0f;
        _visibilityRangeSlider.MaxValue = _visibilityRanges.Count - 1;
    }

    public override void Enter(object[] parameters)
    {
        _enterVisibilityRange = Configuration.ClientConfiguration.Preferences.VisibilityRange;
        if (GameManager.Runtime?.Client is { IsClosed: false })
        {
            _visibilityRangeSlider.MaxValue = _visibilityRanges.FindLastIndex(range =>
                range <= NetworkTerrainPolicy.DefaultMaxClientVisibilityRange
            );
        }
    }

    public override void Update()
    {
        if (_resolutionButton.IsClicked)
        {
            var enumValues = EnumUtils.GetEnumValues(typeof(ResolutionMode));
            Configuration.ClientConfiguration.Preferences.ResolutionMode =
                (ResolutionMode)((enumValues.IndexOf(
                                      (int)Configuration.ClientConfiguration.Preferences.ResolutionMode
                                  ) +
                                  1) %
                                 enumValues.Count);
        }

        if (_visibilityRangeSlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.VisibilityRange =
                _visibilityRanges
                    [MathUtils.Clamp((int)_visibilityRangeSlider.Value, 0, _visibilityRanges.Count - 1)];
        }

        if (_terrainMipmapsButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.TerrainMipmapsEnabled =
                !Configuration.ClientConfiguration.Preferences.TerrainMipmapsEnabled;
        }

        if (_skyRenderingModeButton.IsClicked)
        {
            var enumValues3 = EnumUtils.GetEnumValues(typeof(SkyRenderingMode));
            Configuration.ClientConfiguration.Preferences.SkyRenderingMode =
                (SkyRenderingMode)((enumValues3.IndexOf(
                                        (int)Configuration.ClientConfiguration.Preferences.SkyRenderingMode
                                    ) +
                                    1) %
                                   enumValues3.Count);
        }

        if (_objectShadowsButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.ObjectsShadowsEnabled =
                !Configuration.ClientConfiguration.Preferences.ObjectsShadowsEnabled;
        }

        if (_framerateLimitButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.VSync =
                !Configuration.ClientConfiguration.Preferences.VSync;
        }

        _resolutionButton.Text =
            Localization.ClientLanguage.Catalog.Get(
                "ResolutionMode",
                Configuration.ClientConfiguration.Preferences.ResolutionMode.ToString()
            );
        _visibilityRangeSlider.Value =
            _visibilityRanges.IndexOf(Configuration.ClientConfiguration.Preferences.VisibilityRange) >= 0
                ? _visibilityRanges.IndexOf(Configuration.ClientConfiguration.Preferences.VisibilityRange)
                : 64;
        _visibilityRangeSlider.Text =
            string.Format(
                Localization.ClientLanguage.Catalog.Get(_typeName, 1),
                Configuration.ClientConfiguration.Preferences.VisibilityRange
            );
        if (Configuration.ClientConfiguration.Preferences.VisibilityRange <= 48)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = Localization.ClientLanguage.Catalog.Get(_typeName, 2);
        }
        else if (Configuration.ClientConfiguration.Preferences.VisibilityRange <= 64)
        {
            _visibilityRangeWarningLabel.IsVisible = false;
        }
        else if (Configuration.ClientConfiguration.Preferences.VisibilityRange <= 112)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = Localization.ClientLanguage.Catalog.Get(_typeName, 3);
        }
        else if (Configuration.ClientConfiguration.Preferences.VisibilityRange <= 224)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = Localization.ClientLanguage.Catalog.Get(_typeName, 4);
        }
        else if (Configuration.ClientConfiguration.Preferences.VisibilityRange <= 384)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = Localization.ClientLanguage.Catalog.Get(_typeName, 5);
        }
        else if (Configuration.ClientConfiguration.Preferences.VisibilityRange <= 512)
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = Localization.ClientLanguage.Catalog.Get(_typeName, 6);
        }
        else
        {
            _visibilityRangeWarningLabel.IsVisible = true;
            _visibilityRangeWarningLabel.Text = Localization.ClientLanguage.Catalog.Get(_typeName, 7);
        }

        _terrainMipmapsButton.Text = Configuration.ClientConfiguration.Preferences.TerrainMipmapsEnabled
            ? Localization.ClientLanguage.Catalog.Enable
            : Localization.ClientLanguage.Catalog.Disable;

        _skyRenderingModeButton.Text = Localization.ClientLanguage.Catalog.Get(
            "SkyRenderingMode",
            Configuration.ClientConfiguration.Preferences.SkyRenderingMode.ToString()
        );

        _objectShadowsButton.Text = Configuration.ClientConfiguration.Preferences.ObjectsShadowsEnabled
            ? Localization.ClientLanguage.Catalog.Enable
            : Localization.ClientLanguage.Catalog.Disable;

        _framerateLimitButton.Text = Configuration.ClientConfiguration.Preferences.VSync
            ? Localization.ClientLanguage.Catalog.Get(_typeName, 11)
            : Localization.ClientLanguage.Catalog.Get(_typeName, 8);

        if (Input is { Back: false, Cancel: false } && !Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            return;
        }

        var flag = Configuration.ClientConfiguration.Preferences.VisibilityRange > 128;
        if (Configuration.ClientConfiguration.Preferences.VisibilityRange > _enterVisibilityRange && flag)
        {
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    Localization.ClientLanguage.Catalog.Get(_typeName, 9),
                    Localization.ClientLanguage.Catalog.Get(_typeName, 10),
                    Localization.ClientLanguage.Catalog.Ok,
                    Localization.ClientLanguage.Catalog.Back,
                    delegate(MessageDialogButton button)
                    {
                        if (button != MessageDialogButton.Button1)
                        {
                            return;
                        }

                        ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
                    }
                )
            );
        }
        else
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
