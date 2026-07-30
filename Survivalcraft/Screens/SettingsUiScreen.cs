using System.Xml.Linq;

using Game.Commands;

namespace Game.Screens;

public class SettingsUiScreen : Screen
{
    private const string _typeName = nameof(SettingsUiScreen);

    private readonly ButtonWidget _communityContentModeButton;

    private readonly ButtonWidget _hideMoveLookPadsButton;

    private readonly ButtonWidget _languageButton;

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
        _languageButton = Children.Find<BevelledButtonWidget>("LanguageButton")!;
        _windowModeButton = Children.Find<ButtonWidget>("WindowModeButton")!;
        _uiScaleSlider = Children.Find<SliderWidget>("UIScaleSlider")!;
        _upsideDownButton = Children.Find<ButtonWidget>("UpsideDownButton")!;
        _hideMoveLookPadsButton = Children.Find<ButtonWidget>("HideMoveLookPads")!;
        _showGuiInScreenshotsButton = Children.Find<ButtonWidget>("ShowGuiInScreenshotsButton")!;
        _showLogoInScreenshotsButton = Children.Find<ButtonWidget>("ShowLogoInScreenshotsButton")!;
        _screenshotSizeButton = Children.Find<ButtonWidget>("ScreenshotSizeButton")!;
        _communityContentModeButton = Children.Find<ButtonWidget>("CommunityContentModeButton")!;
    }

    public override void Enter(object[] parameters)
    {
        _windowModeContainer.IsVisible = true;
    }

    public override void Update()
    {
        if (GameManager.Project != null)
        {
            GameManager.UpdateProject();
        }

        if (_windowModeButton.IsClicked)
        {
            SettingsManager.Current.WindowMode = (WindowMode)((int)(SettingsManager.Current.WindowMode + 1) %
                                                      EnumUtils.GetEnumValues(typeof(WindowMode)).Count);
        }

        if (_uiScaleSlider.SlidingCompleted)
        {
            SettingsManager.Current.UIScale = _uiScaleSlider.Value;
        }

        if (_languageButton.IsClicked)
        {
            OnLanguageButtonClick(); // 调用新的语言选择功能
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

        if (_hideMoveLookPadsButton.IsClicked)
        {
            SettingsManager.Current.HideMoveLookPads = !SettingsManager.Current.HideMoveLookPads;
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
            SettingsManager.Current.ScreenshotSize = (ScreenshotSize)((int)(SettingsManager.Current.ScreenshotSize + 1) %
                                                              EnumUtils.GetEnumValues(typeof(ScreenshotSize)).Count);
        }

        if (_communityContentModeButton.IsClicked)
        {
            SettingsManager.Current.CommunityContentMode =
                (CommunityContentMode)((int)(SettingsManager.Current.CommunityContentMode + 1) %
                                       EnumUtils.GetEnumValues(typeof(CommunityContentMode)).Count);
        }

        // 更新按钮文本
        _windowModeButton.Text = LanguageManager.Get("WindowMode", SettingsManager.Current.WindowMode.ToString());
        _languageButton.Text = LanguageManager.Get("Language", "Name");
        _upsideDownButton.Text = SettingsManager.Current.UpsideDownLayout ? LanguageManager.Yes : LanguageManager.No;
        _hideMoveLookPadsButton.Text = SettingsManager.Current.HideMoveLookPads ? LanguageManager.Yes : LanguageManager.No;
        _showGuiInScreenshotsButton.Text =
            SettingsManager.Current.ShowGuiInScreenshots ? LanguageManager.Yes : LanguageManager.No;
        _showLogoInScreenshotsButton.Text =
            SettingsManager.Current.ShowLogoInScreenshots ? LanguageManager.Yes : LanguageManager.No;
        _screenshotSizeButton.Text = LanguageManager.Get("ScreenshotSize", SettingsManager.Current.ScreenshotSize.ToString());
        _communityContentModeButton.Text =
            LanguageManager.Get("CommunityContentMode", SettingsManager.Current.CommunityContentMode.ToString());

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }

    private static void OnLanguageButtonClick()
    {
        DialogsManager.ShowDialog(
            null,
            new ListSelectionDialog(
                string.Empty,
                LanguageManager.LanguageTypes,
                70f,
                item => LanguageManager.GetLanguageDisplayName((string)item),
                delegate(object item) { ChangeLanguage((string)item); }
            )
        );
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
