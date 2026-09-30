using System.Globalization;
using System.Xml.Linq;

using Game.Commands;

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
        _languageSelection.ItemTextProvider = item => LanguageManager.GetLanguageDisplayName((string)item);
        _languageSelection.SelectionChanged += () =>
        {
            if (_languageSelection.SelectedItem is string languageType &&
                !languageType.Equals(LanguageManager.CurrentLanguage, StringComparison.OrdinalIgnoreCase))
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
        _windowModeContainer.IsVisible = PlatformManager.Platform is not Platform.Android;
        _languageSelection.SetItems(LanguageManager.LanguageTypes);
        _languageSelection.SelectedItem = LanguageManager.CurrentLanguage;
    }

    public override void Update()
    {
        if (GameManager.Project != null)
        {
            GameManager.UpdateProject();
        }

        if (_windowModeButton.IsClicked)
        {
            var windowMode = (WindowMode)(((int)RunningSettingManager.Current.WindowMode + 1) %
                                          EnumUtils.GetEnumValues(typeof(WindowMode)).Count);
            Window.WindowMode = windowMode;
            RunningSettingManager.SaveCurrent(rs => rs.WindowMode = windowMode);
        }

        if (_uiScaleSlider.SlidingCompleted)
        {
            SettingsManager.Current.UIScale = _uiScaleSlider.Value;
        }

        if (!_uiScaleSlider.IsSliding)
        {
            _uiScaleSlider.Value = SettingsManager.Current.UIScale;
        }

        _uiScaleSlider.Text = $"{_uiScaleSlider.Value * 100f:0}%";

        if (_upsideDownButton.IsClicked)
        {
            SettingsManager.Current.UpsideDownLayout = !SettingsManager.Current.UpsideDownLayout;
        }

        if (_hudSafeAreaButton.IsClicked)
        {
            SettingsManager.Current.HudSafeAreaEnabled = !SettingsManager.Current.HudSafeAreaEnabled;
        }

        if (_hudSafeAreaCustomPaddingButton.IsClicked)
        {
            SettingsManager.Current.HudSafeAreaCustomPaddingEnabled =
                !SettingsManager.Current.HudSafeAreaCustomPaddingEnabled;
        }

        if (_hudSafeAreaPaddingSlider.IsSliding)
        {
            SettingsManager.Current.HudSafeAreaPadding = _hudSafeAreaPaddingSlider.Value;
        }

        if (_showGuiInScreenshotsButton.IsClicked)
        {
            SettingsManager.Current.ShowGuiInScreenshots = !SettingsManager.Current.ShowGuiInScreenshots;
        }

        if (_showLogoInScreenshotsButton.IsClicked)
        {
            SettingsManager.Current.ShowLogoInScreenshots = !SettingsManager.Current.ShowLogoInScreenshots;
        }

        if (_screenshotSizeButton.IsClicked)
        {
            SettingsManager.Current.ScreenshotSize =
                (ScreenshotSize)((int)(SettingsManager.Current.ScreenshotSize + 1) %
                                 EnumUtils.GetEnumValues(typeof(ScreenshotSize)).Count);
        }

        // 更新按钮文本
        _windowModeButton.Text = LanguageManager.Get("WindowMode", RunningSettingManager.Current.WindowMode.ToString());
        _upsideDownButton.Text = SettingsManager.Current.UpsideDownLayout ? LanguageManager.Yes : LanguageManager.No;
        _hudSafeAreaButton.Text = SettingsManager.Current.HudSafeAreaEnabled
            ? LanguageManager.Get("Usual", "on")
            : LanguageManager.Get("Usual", "off");
        _hudSafeAreaCustomPaddingButton.IsEnabled = SettingsManager.Current.HudSafeAreaEnabled;
        _hudSafeAreaCustomPaddingButton.Text = SettingsManager.Current.HudSafeAreaCustomPaddingEnabled
            ? LanguageManager.Get("Usual", "on")
            : LanguageManager.Get("Usual", "off");
        _hudSafeAreaPaddingSlider.IsEnabled = SettingsManager.Current.HudSafeAreaEnabled &&
                                              SettingsManager.Current.HudSafeAreaCustomPaddingEnabled;
        var hudSafeAreaPadding = SettingsManager.Current.HudSafeAreaCustomPaddingEnabled
            ? SettingsManager.Current.HudSafeAreaPadding
            : HudSafeAreaManager.GetAutomaticPadding(_hudSafeAreaPaddingSlider.GlobalScale);
        if (!_hudSafeAreaPaddingSlider.IsSliding)
        {
            _hudSafeAreaPaddingSlider.Value = hudSafeAreaPadding;
        }

        _hudSafeAreaPaddingSlider.Text = MathUtils.Round(hudSafeAreaPadding)
            .ToString(CultureInfo.InvariantCulture);
        _showGuiInScreenshotsButton.Text =
            SettingsManager.Current.ShowGuiInScreenshots ? LanguageManager.Yes : LanguageManager.No;
        _showLogoInScreenshotsButton.Text =
            SettingsManager.Current.ShowLogoInScreenshots ? LanguageManager.Yes : LanguageManager.No;
        _screenshotSizeButton.Text =
            LanguageManager.Get("ScreenshotSize", SettingsManager.Current.ScreenshotSize.ToString());
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

        var result = CommandExecutor.ExecuteApplication(
            new SetLanguageCommand(languageType),
            GameManager.Project);
        if (!result.Success)
        {
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    LanguageManager.Error,
                    CommandText.Resolve(result),
                    LanguageManager.Ok));
        }
    }

}
