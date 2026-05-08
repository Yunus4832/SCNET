using System.Xml.Linq;

namespace Game.Screens;

public class SettingsUiScreen : Screen
{
    private const string _typeName = "SettingsUiScreen";

    private readonly ButtonWidget _communityContentModeButton;

    private readonly ButtonWidget _displayLogButton;

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
        _displayLogButton = Children.Find<ButtonWidget>("DisplayLogButton")!;
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
            SettingsManager.WindowMode = (WindowMode)((int)(SettingsManager.WindowMode + 1) %
                                                      EnumUtils.GetEnumValues(typeof(WindowMode)).Count);
        }

        if (_uiScaleSlider.SlidingCompleted)
        {
            SettingsManager.UIScale = _uiScaleSlider.Value;
        }

        if (_languageButton.IsClicked)
        {
            OnLanguageButtonClick(); // 调用新的语言选择功能
        }

        if (_displayLogButton.IsClicked)
        {
            SettingsManager.DisplayLog = !SettingsManager.DisplayLog;
        }

        if (!_uiScaleSlider.IsSliding)
        {
            _uiScaleSlider.Value = SettingsManager.UIScale;
        }

        _uiScaleSlider.Text = $"{_uiScaleSlider.Value * 100f:0}%";

        if (_upsideDownButton.IsClicked)
        {
            SettingsManager.UpsideDownLayout = !SettingsManager.UpsideDownLayout;
        }

        if (_hideMoveLookPadsButton.IsClicked)
        {
            SettingsManager.HideMoveLookPads = !SettingsManager.HideMoveLookPads;
        }

        if (_showGuiInScreenshotsButton.IsClicked)
        {
            SettingsManager.ShowGuiInScreenshots = !SettingsManager.ShowGuiInScreenshots;
        }

        if (_showLogoInScreenshotsButton.IsClicked)
        {
            SettingsManager.ShowLogoInScreenshots = !SettingsManager.ShowLogoInScreenshots;
        }

        if (_screenshotSizeButton.IsClicked)
        {
            SettingsManager.ScreenshotSize = (ScreenshotSize)((int)(SettingsManager.ScreenshotSize + 1) %
                                                              EnumUtils.GetEnumValues(typeof(ScreenshotSize)).Count);
        }

        if (_communityContentModeButton.IsClicked)
        {
            SettingsManager.CommunityContentMode =
                (CommunityContentMode)((int)(SettingsManager.CommunityContentMode + 1) %
                                       EnumUtils.GetEnumValues(typeof(CommunityContentMode)).Count);
        }

        // 更新按钮文本
        _windowModeButton.Text = LanguageControl.Get("WindowMode", SettingsManager.WindowMode.ToString());
        _languageButton.Text = LanguageControl.Get("Language", "Name");
        _displayLogButton.Text = SettingsManager.DisplayLog ? LanguageControl.Yes : LanguageControl.No;
        _upsideDownButton.Text = SettingsManager.UpsideDownLayout ? LanguageControl.Yes : LanguageControl.No;
        _hideMoveLookPadsButton.Text = SettingsManager.HideMoveLookPads ? LanguageControl.Yes : LanguageControl.No;
        _showGuiInScreenshotsButton.Text =
            SettingsManager.ShowGuiInScreenshots ? LanguageControl.Yes : LanguageControl.No;
        _showLogoInScreenshotsButton.Text =
            SettingsManager.ShowLogoInScreenshots ? LanguageControl.Yes : LanguageControl.No;
        _screenshotSizeButton.Text = LanguageControl.Get("ScreenshotSize", SettingsManager.ScreenshotSize.ToString());
        _communityContentModeButton.Text =
            LanguageControl.Get("CommunityContentMode", SettingsManager.CommunityContentMode.ToString());

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
                LanguageControl.LanguageTypes,
                70f,
                item => (string)item,
                delegate(object item) { ChangeLanguage((string)item); }
            )
        );
    }

    public static void ChangeLanguage(string languageType)
    {
        // 确保语言类型有效
        if (string.IsNullOrEmpty(languageType))
        {
            Log.Warning("无效的语言类型: " + languageType);
            return;
        }

        // 初始化语言
        LanguageControl.Initialize(languageType);

        // 加载所有插件的语言
        foreach (var mod in ModsManager.ModList)
        {
            mod.LoadLanguage();
        }

        LanguageControl.RefreshCommonWords();

        // 重新实例化屏幕对象
        var objs = new Dictionary<string, object>();
        foreach (var screen in ScreensManager.Screens)
        {
            var type = screen.Value.GetType();
            try
            {
                var obj = Activator.CreateInstance(type);
                if (obj != null)
                {
                    objs.Add(screen.Key, obj);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"无法实例化屏幕对象,语言文件错误 {type.FullName}: {ex.Message}");
            }
        }

        // 将重新实例化的屏幕对象赋值回 ScreensManager._screens
        foreach (var obj in objs)
        {
            if (obj.Value is not Screen screen)
            {
                continue;
            }

            ScreensManager.Screens[obj.Key] = screen;
        }

        // 初始化配方管理器
        CraftingRecipesManager.Initialize();

        // 切换到主菜单
        ScreensManager.SwitchScreen("MainMenu");
    }
}
