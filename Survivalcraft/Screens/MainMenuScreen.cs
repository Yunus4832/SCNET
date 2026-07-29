using System.Xml.Linq;

using Engine.Input;

using Game.Commands;
using Game.Network;

namespace Game.Screens;

public class MainMenuScreen : Screen
{
    private readonly StackPanelWidget _bulletinStackPanel;

    private readonly LabelWidget _copyrightLabel;

    private readonly BevelledButtonWidget _languageSwitchButton;

    private readonly BevelledButtonWidget _serverModeButton;

    private readonly ButtonWidget _showBulletinButton;

    private static readonly string _versionString = $"Version {VersionsManager.Version}";

    public MainMenuScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/MainMenuScreen");
        LoadContents(this, node);
        _showBulletinButton = Children.Find<ButtonWidget>("BulletinButton")!;
        _bulletinStackPanel = Children.Find<StackPanelWidget>("BulletinStackPanel")!;
        _copyrightLabel = Children.Find<LabelWidget>("CopyrightLabel")!;
        _serverModeButton = Children.Find<BevelledButtonWidget>("ServerModeButton")!;
        _languageSwitchButton = Children.Find<BevelledButtonWidget>("LanguageButton")!;

        // 绑定语言切换按钮的点击事件
        _languageSwitchButton.ClickableWidget.OnClick += OnLanguageButtonClick;
        _serverModeButton.ClickableWidget.OnClick += OnServerModeButtonClick;

        // 初始化语言相关 UI 状态
        var languageType = !AppConfigStore.Values.TryGetValue("Language", out var config) ? "zh-CN" : config;
        _bulletinStackPanel.IsVisible = languageType == "zh-CN";
        _copyrightLabel.IsVisible = languageType != "zh-CN";
    }

    public override void Enter(object[] parameters)
    {
        Children.Find<MotdWidget>(false)?.Restart();

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
                LanguageManager.LanguageTypes,
                70f,
                item => LanguageManager.GetLanguageDisplayName((string)item),
                delegate(object item)
                {
                    // 用户选择语言后调用 ChangeLanguage 方法
                    SettingsUiScreen.ChangeLanguage((string)item);
                }
            )
        );
    }

    private static void OnServerModeButtonClick()
    {
        DialogsManager.Confirm(LanguageManager.Get("MainMenuScreen", 13), button =>
        {
            if (button != MessageDialogButton.Button1)
            {
                return;
            }

            var result = CommandExecutor.ExecuteLocalHost(
                new SetRunModeCommand(RunModeType.HeadlessServer),
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
        });
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

        if (_showBulletinButton.IsClicked)
        {
            if (string.IsNullOrEmpty(MotdManager.BulletinDefault.Content) ||
                string.Equals(MotdManager.BulletinDefault.Title.ToLower(), "null", StringComparison.Ordinal))
            {
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        "公告获取失败", "当前暂无发布公告，\n或者没有联网获取公告信息",
                        LanguageManager.Ok
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

        var exitRequested =
            Children.Find<ButtonWidget>("Exit")!.IsClicked ||
            (Input.Back && !Keyboard.BackButtonQuitsApp) ||
            Input.IsKeyDownOnce(Key.Escape);
        if (exitRequested)
        {
            ConfirmExit();
        }
    }

    private static void ConfirmExit()
    {
        DialogsManager.Confirm(LanguageManager.Get("MainMenuScreen", 14), button =>
        {
            if (button is MessageDialogButton.Button1)
            {
                Window.Close();
            }
        });
    }
}
