using System.Xml.Linq;
using Engine.Input;
using Game.NetWork;
using Game.VersionConverts;

namespace Game.Screens;

public class MainMenuScreen : Screen
{
    private readonly StackPanelWidget _bulletinStackPanel;

    private readonly LabelWidget _copyrightLabel;

    private readonly BitmapButtonWidget _languageSwitchButton;

    private readonly ButtonWidget _showBulletinButton;

    private static readonly string _versionString = $"Version {VersionsManager.Version}";

    public MainMenuScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/MainMenuScreen");
        LoadContents(this, node);
        _showBulletinButton = Children.Find<ButtonWidget>("BulletinButton")!;
        _bulletinStackPanel = Children.Find<StackPanelWidget>("BulletinStackPanel")!;
        _copyrightLabel = Children.Find<LabelWidget>("CopyrightLabel")!;
        _languageSwitchButton = Children.Find<BitmapButtonWidget>("LanguageButton")!;

        // 绑定语言切换按钮的点击事件
        _languageSwitchButton.ClickableWidget.ClickAction += OnLanguageButtonClick;

        // 初始化语言相关 UI 状态
        var languageType = !ModsManager.Configs.TryGetValue("Language", out var config) ? "zh-CN" : config;
        _bulletinStackPanel.IsVisible = languageType == "zh-CN";
        _copyrightLabel.IsVisible = languageType != "zh-CN";
    }

    public override void Enter(object[] parameters)
    {
        Children.Find<MotdWidget>(false)?.Restart();
        // 检查是否需要迁移数据
        if (SettingsManager.IsolatedStorageMigrationCounter < 3)
        {
            SettingsManager.IsolatedStorageMigrationCounter++;
            VersionConverter126To127.MigrateDataFromIsolatedStorageWithDialog();
        }

        // 如果当前已连接网络，则停止连接
        if (CommonLib.Net.CurrentStage == NetNode.Stage.Connected)
        {
            CommonLib.Net.Stop();
        }

        // 显示公告（如果允许）
        if (MotdManager.CanShowBulletin)
        {
            MotdManager.ShowBulletin();
        }
    }

    public override void Leave()
    {
        Keyboard.BackButtonQuitsApp = false;
    }

    private static void OnLanguageButtonClick()
    {
        // 显示语言选择对话框
        DialogsManager.ShowDialog(
            null,
            new ListSelectionDialog(
                string.Empty,
                LanguageControl.LanguageTypes,
                70f,
                item => (string)item,
                delegate(object item)
                {
                    // 用户选择语言后调用 ChangeLanguage 方法
                    SettingsUiScreen.ChangeLanguage((string)item);
                }
            )
        );
    }

    public override void Update()
    {
        // 更新版本号显示
        Children.Find<LabelWidget>("Version")!.Text = _versionString;

        // 动态调整 Logo 大小
        var rectangleWidget = Children.Find<RectangleWidget>("Logo")!;
        var scale = 1f + 0.02f * MathUtils.Sin(1.5f * (float)MathUtils.Remainder(Time.FrameStartTime, 10000.0));
        rectangleWidget.RenderTransform =
            Matrix.CreateTranslation((0f - rectangleWidget.ActualSize.X) / 2f, (0f - rectangleWidget.ActualSize.Y) / 2f,
                0f) * Matrix.CreateScale(scale, scale, 1f) * Matrix.CreateTranslation(rectangleWidget.ActualSize.X / 2f,
                rectangleWidget.ActualSize.Y / 2f, 0f);

        // 处理按钮点击事件
        if (Children.Find<ButtonWidget>("Play")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Play");
        }

        if (Children.Find<ButtonWidget>("Help")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Help");
        }

        if (Children.Find<ButtonWidget>("Content")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Content");
        }

        if (Children.Find<ButtonWidget>("Settings")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Settings");
        }

        if (Children.Find<ButtonWidget>("LanguageButton")!.IsClicked)
        {
            var languageMap = new Dictionary<string, string>
            {
                { "简体中文", "zh-CN" },
                { "English", "en-US" },
                { "Русский", "ru-RU" },
                { "Português", "pt-PT" }
            };
            var displayItems = languageMap.Keys.ToList();

            // 显示语言选择对话框
            DialogsManager.ShowDialog(
                null,
                new ListSelectionDialog(
                    string.Empty,
                    displayItems,
                    70f,
                    item => (string)item,
                    delegate(object item)
                    {
                        // 用户选择语言后调用 ChangeLanguage 方法
                        if (languageMap.TryGetValue((string)item, out var code))
                        {
                            SettingsUiScreen.ChangeLanguage(code);
                        }
                    }
                )
            );
        }

        if (Children.Find<BevelledButtonWidget>("Manage")!.IsClicked)
        {
            ScreensManager.Screens.TryGetValue("Content", out var screen);
            var contentScreen = screen as ContentScreen;
            contentScreen?.OpenManageSelectDialog();
        }

        if (_showBulletinButton.IsClicked)
        {
            if (string.IsNullOrEmpty(MotdManager.BulletinDefault.Content) ||
                string.Equals(MotdManager.BulletinDefault.Title.ToLower(), "null", StringComparison.Ordinal))
            {
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        "公告获取失败", "当前暂无发布公告，\n或者没有联网获取公告信息",
                        LanguageControl.Ok
                    )
                );
            }
            else
            {
                MotdManager.ShowBulletin();
            }
        }

        if (Children.Find<ButtonWidget>("Online")!.IsClicked)
        {
            ScreensManager.SwitchScreen("NetPlay");
        }

        if (Children.Find<ButtonWidget>("Exit")!.IsClicked)
        {
            Window.Close();
        }

        // 处理返回键或 ESC 键
        if ((Input.Back && !Keyboard.BackButtonQuitsApp) || Input.IsKeyDownOnce(Key.Escape))
        {
            Window.Close();
        }
    }
}
