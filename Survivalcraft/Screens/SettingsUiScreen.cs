using System.Globalization;
using System.Xml.Linq;

using Game.Application;
using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Platforms;

namespace Game.Screens;

public class SettingsUiScreen : Screen
{
    private const string _typeName = nameof(SettingsUiScreen);

    private readonly InlineSelectionWidget _languageSelection;

    private readonly ButtonWidget _hudSafeAreaButton;

    private readonly ButtonWidget _hudSafeAreaCustomPaddingButton;

    private readonly SliderWidget _hudSafeAreaPaddingSlider;

    private readonly ButtonWidget _screenshotSizeButton;

    private readonly ButtonWidget _showGuiInScreenshotsButton;

    private readonly ButtonWidget _showLogoInScreenshotsButton;

    private readonly SliderWidget _uiScaleSlider;

    private readonly ButtonWidget _upsideDownButton;

    private readonly ButtonWidget _windowModeButton;

    private readonly ContainerWidget _windowModeContainer;


    public SettingsUiScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/SettingsUiScreen");
        LoadContents(this, node);
        _windowModeContainer = Children.Find<ContainerWidget>("WindowModeContainer")!;
        _languageSelection = Children.Find<InlineSelectionWidget>("LanguageSelection")!;
        _languageSelection.ItemTextProvider = item =>
            Localization.ClientLanguage.Catalog.GetLanguageDisplayName((string)item);
        _languageSelection.SelectionChanged += () =>
        {
            if (_languageSelection.SelectedItem is string languageType &&
                !languageType.Equals(
                    Localization.ClientLanguage.Catalog.CurrentLanguage,
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                ChangeLanguage(languageType);
            }
        };
        _hudSafeAreaButton = Children.Find<ButtonWidget>("HudSafeArea")!;
        _hudSafeAreaCustomPaddingButton = Children.Find<ButtonWidget>("HudSafeAreaCustomPadding")!;
        _hudSafeAreaPaddingSlider = Children.Find<SliderWidget>("HudSafeAreaPadding")!;
        _windowModeButton = Children.Find<ButtonWidget>("WindowModeButton")!;
        _uiScaleSlider = Children.Find<SliderWidget>("UIScaleSlider")!;
        _upsideDownButton = Children.Find<ButtonWidget>("UpsideDownButton")!;
        _showGuiInScreenshotsButton = Children.Find<ButtonWidget>("ShowGuiInScreenshotsButton")!;
        _showLogoInScreenshotsButton = Children.Find<ButtonWidget>("ShowLogoInScreenshotsButton")!;
        _screenshotSizeButton = Children.Find<ButtonWidget>("ScreenshotSizeButton")!;
    }

    public override void Enter(object[] parameters)
    {
        _windowModeContainer.IsVisible = ClientPlatform.Kind is not Platform.Android;
        _languageSelection.SetItems(Localization.ClientLanguage.Catalog.LanguageTypes);
        _languageSelection.SelectedItem = Localization.ClientLanguage.Catalog.CurrentLanguage;
    }

    public override void Update()
    {
        if (_windowModeButton.IsClicked)
        {
            var windowMode = (StartupWindowMode)(((int)StartupManager.Profiles.CurrentRunningSettings.WindowMode + 1) %
                                                 Enum.GetValues<StartupWindowMode>().Length);
            Window.WindowMode = StartupWindowModes.ToWindowMode(windowMode);
            StartupManager.Profiles.UpdateRunningSettings(rs => rs.WindowMode = windowMode);
        }

        if (_uiScaleSlider.SlidingCompleted)
        {
            Configuration.ClientConfiguration.Preferences.UIScale = _uiScaleSlider.Value;
        }

        if (!_uiScaleSlider.IsSliding)
        {
            _uiScaleSlider.Value = Configuration.ClientConfiguration.Preferences.UIScale;
        }

        _uiScaleSlider.Text = $"{_uiScaleSlider.Value * 100f:0}%";

        if (_upsideDownButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.UpsideDownLayout =
                !Configuration.ClientConfiguration.Preferences.UpsideDownLayout;
        }

        if (_hudSafeAreaButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.HudSafeAreaEnabled =
                !Configuration.ClientConfiguration.Preferences.HudSafeAreaEnabled;
        }

        if (_hudSafeAreaCustomPaddingButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.HudSafeAreaCustomPaddingEnabled =
                !Configuration.ClientConfiguration.Preferences.HudSafeAreaCustomPaddingEnabled;
        }

        if (_hudSafeAreaPaddingSlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.HudSafeAreaPadding = _hudSafeAreaPaddingSlider.Value;
        }

        if (_showGuiInScreenshotsButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.ShowGuiInScreenshots =
                !Configuration.ClientConfiguration.Preferences.ShowGuiInScreenshots;
        }

        if (_showLogoInScreenshotsButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.ShowLogoInScreenshots =
                !Configuration.ClientConfiguration.Preferences.ShowLogoInScreenshots;
        }

        if (_screenshotSizeButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.ScreenshotSize =
                (ScreenshotSize)((int)(Configuration.ClientConfiguration.Preferences.ScreenshotSize + 1) %
                                 EnumUtils.GetEnumValues(typeof(ScreenshotSize)).Count);
        }

        // 更新按钮文本
        _windowModeButton.Text = Localization.ClientLanguage.Catalog.Get(
            "WindowMode",
            StartupManager.Profiles.CurrentRunningSettings.WindowMode.ToString()
        );
        _upsideDownButton.Text = Configuration.ClientConfiguration.Preferences.UpsideDownLayout
            ? Localization.ClientLanguage.Catalog.Yes
            : Localization.ClientLanguage.Catalog.No;
        _hudSafeAreaButton.Text = Configuration.ClientConfiguration.Preferences.HudSafeAreaEnabled
            ? Localization.ClientLanguage.Catalog.Get("Usual", "on")
            : Localization.ClientLanguage.Catalog.Get("Usual", "off");
        _hudSafeAreaCustomPaddingButton.IsEnabled =
            Configuration.ClientConfiguration.Preferences.HudSafeAreaEnabled;
        _hudSafeAreaCustomPaddingButton.Text =
            Configuration.ClientConfiguration.Preferences.HudSafeAreaCustomPaddingEnabled
                ? Localization.ClientLanguage.Catalog.Get("Usual", "on")
                : Localization.ClientLanguage.Catalog.Get("Usual", "off");
        _hudSafeAreaPaddingSlider.IsEnabled = Configuration.ClientConfiguration.Preferences.HudSafeAreaEnabled &&
                                              Configuration.ClientConfiguration.Preferences
                                                  .HudSafeAreaCustomPaddingEnabled;
        var hudSafeAreaPadding = Configuration.ClientConfiguration.Preferences.HudSafeAreaCustomPaddingEnabled
            ? Configuration.ClientConfiguration.Preferences.HudSafeAreaPadding
            : HudSafeAreaManager.GetAutomaticPadding(_hudSafeAreaPaddingSlider.GlobalScale);
        if (!_hudSafeAreaPaddingSlider.IsSliding)
        {
            _hudSafeAreaPaddingSlider.Value = hudSafeAreaPadding;
        }

        _hudSafeAreaPaddingSlider.Text = MathUtils.Round(hudSafeAreaPadding)
            .ToString(CultureInfo.InvariantCulture);
        _showGuiInScreenshotsButton.Text =
            Configuration.ClientConfiguration.Preferences.ShowGuiInScreenshots
                ? Localization.ClientLanguage.Catalog.Yes
                : Localization.ClientLanguage.Catalog.No;
        _showLogoInScreenshotsButton.Text =
            Configuration.ClientConfiguration.Preferences.ShowLogoInScreenshots
                ? Localization.ClientLanguage.Catalog.Yes
                : Localization.ClientLanguage.Catalog.No;
        _screenshotSizeButton.Text =
            Localization.ClientLanguage.Catalog.Get(
                "ScreenshotSize",
                Configuration.ClientConfiguration.Preferences.ScreenshotSize.ToString()
            );
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }

    public static void ChangeLanguage(string languageType)
    {
        if (string.IsNullOrEmpty(languageType))
        {
            Log.Warning("无效的语言类型: " + languageType);
            return;
        }

        var result = CommandExecutor.ExecuteLocal(
            CurrentModRuntime.Value?.Commands,
            new SetLanguageCommand(languageType),
            ClientApplication.World?.Project
        );
        if (!result.Success)
        {
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    Localization.ClientLanguage.Catalog.Error,
                    CommandText.Resolve(result),
                    Localization.ClientLanguage.Catalog.Ok
                )
            );
        }
    }
}
