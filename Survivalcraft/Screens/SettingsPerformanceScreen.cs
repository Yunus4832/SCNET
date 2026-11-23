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

    private readonly InlineSelectionWidget _resolutionSelection;

    private readonly InlineSelectionWidget _skyRenderingModeSelection;

    private readonly ButtonWidget _terrainMipmapsButton;

    private readonly SliderWidget _visibilityRangeSlider;

    private readonly LabelWidget _visibilityRangeWarningLabel;

    public SettingsPerformanceScreen()
    {
        var node = AssetManager.Get<XElement>("Screens/SettingsPerformanceScreen");
        LoadContents(this, node);
        _resolutionSelection = Children.Find<InlineSelectionWidget>("ResolutionButton")!;
        _resolutionSelection.ConfigureEnum<ResolutionMode>(
            item => Localization.ClientLanguage.Catalog.Get("ResolutionMode", item.ToString()),
            value => Configuration.ClientConfiguration.Preferences.ResolutionMode = value
        );
        _visibilityRangeSlider = Children.Find<SliderWidget>("VisibilityRangeSlider")!;
        _visibilityRangeWarningLabel = Children.Find<LabelWidget>("VisibilityRangeWarningLabel")!;
        _terrainMipmapsButton = Children.Find<ButtonWidget>("TerrainMipmapsButton")!;
        _skyRenderingModeSelection = Children.Find<InlineSelectionWidget>("SkyRenderingModeButton")!;
        _skyRenderingModeSelection.ConfigureEnum<SkyRenderingMode>(
            item => Localization.ClientLanguage.Catalog.Get("SkyRenderingMode", item.ToString()),
            value => Configuration.ClientConfiguration.Preferences.SkyRenderingMode = value
        );
        _objectShadowsButton = Children.Find<ButtonWidget>("ObjectShadowsButton")!;
        _framerateLimitButton = Children.Find<ButtonWidget>("FramerateLimitButton")!;
        _visibilityRangeSlider.MinValue = 0f;
        _visibilityRangeSlider.MaxValue = _visibilityRanges.Count - 1;
    }

    public override void Enter(object[] parameters)
    {
        _resolutionSelection.SelectedItem = Configuration.ClientConfiguration.Preferences.ResolutionMode;
        _skyRenderingModeSelection.SelectedItem = Configuration.ClientConfiguration.Preferences.SkyRenderingMode;
        _enterVisibilityRange = Configuration.ClientConfiguration.Preferences.VisibilityRange;
        if (GameManager.Runtime?.Client is { IsClosed: false })
        {
            _visibilityRangeSlider.MaxValue = _visibilityRanges.FindLastIndex(range =>
                range <= NetworkTerrainPolicy.DefaultMaxClientVisibilityRange
            );
        }
    }

    public override void Leave()
    {
        _resolutionSelection.Close();
        _skyRenderingModeSelection.Close();
    }

    public override void Update()
    {
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

        var usesExtendedVisibility = Configuration.ClientConfiguration.Preferences.VisibilityRange > 128;
        if (Configuration.ClientConfiguration.Preferences.VisibilityRange > _enterVisibilityRange &&
            usesExtendedVisibility)
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
