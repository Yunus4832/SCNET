using System.Xml.Linq;

using Engine.Input;

using Game.Commands;
using Game.Network;

namespace Game.Screens;

public class MainMenuScreen : Screen
{
    private readonly VerticalTabMenuWidget _mainMenuTabs;

    private readonly RectangleWidget _logo;

    private readonly RectangleWidget _musicButtonIcon;

    private readonly ClickableWidget _musicButtonClickable;

    private bool _isMenuMusicEnabled = true;

    private float _musicButtonAngle;

    private static readonly string _versionString = $"Version {VersionsManager.Version}";

    public MainMenuScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/MainMenuScreen");
        LoadContents(this, node);
        _mainMenuTabs = Children.Find<VerticalTabMenuWidget>("MainMenuTabs")!;
        _logo = Children.Find<RectangleWidget>("Logo")!;
        _musicButtonIcon = Children.Find<RectangleWidget>("MusicButton.Icon")!;
        _musicButtonClickable = Children.Find<ClickableWidget>("MusicButton.Clickable")!;
        Children.Find<LabelWidget>("Version")!.Text = _versionString;

        ConfigureMainMenuTabs();
    }

    public override void Enter(object[] parameters)
    {
        MusicManager.CurrentMix = _isMenuMusicEnabled ? MusicManager.Mix.Menu : MusicManager.Mix.None;

        // 如果当前已连接网络，则停止连接
        if (CommonLib.Net.CurrentStage == NetNode.Stage.Connected)
        {
            CommonLib.Net.Stop();
        }
    }

    public override void Leave()
    {
        Keyboard.BackButtonQuitsApp = false;
    }

    private void ConfigureMainMenuTabs()
    {
        _mainMenuTabs.AddTab(new VerticalTabMenu(
            "Textures/Atlas/Exit",
            () =>
            [
                new VerticalTabMenuItem(MainMenuText("Restart"),
                    () => ExecutePowerMenuAction(PowerMenuAction.Restart)),
                new VerticalTabMenuItem(MainMenuText("SwitchInstance"),
                    () => ExecutePowerMenuAction(PowerMenuAction.SwitchInstance)),
                new VerticalTabMenuItem(MainMenuText("RestartHeadless"),
                    () => ExecutePowerMenuAction(PowerMenuAction.RestartHeadless)),
                new VerticalTabMenuItem(MainMenuText("Exit"),
                    () => ExecutePowerMenuAction(PowerMenuAction.Exit))
            ],
            new Vector2(180f, 44f)));
        _mainMenuTabs.AddNavigationTab(
            "Textures/Atlas/Instance",
            () => ScreensManager.SwitchScreen("InstanceManagement"));
        _mainMenuTabs.AddTab(new VerticalTabMenu(
            "Textures/Atlas/Earth",
            () => LanguageManager.LanguageTypes
                .Select(languageType => new VerticalTabMenuItem(
                    LanguageManager.GetLanguageDisplayName(languageType),
                    () => SettingsUiScreen.ChangeLanguage(languageType)))
                .ToArray(),
            new Vector2(180f, 44f)));
    }

    private static void ExecutePowerMenuAction(PowerMenuAction action)
    {
        switch (action)
        {
            case PowerMenuAction.Restart:
                ConfirmAction(MainMenuText("ConfirmRestart"), () =>
                    ExecuteApplicationCommand(new RestartApplicationCommand(new SessionInfo
                    {
                        Target = SessionTarget.MainMenu
                    })));
                break;
            case PowerMenuAction.SwitchInstance:
                ShowInstanceMenu();
                break;
            case PowerMenuAction.RestartHeadless:
                ShowHeadlessTargetMenu();
                break;
            case PowerMenuAction.Exit:
                ConfirmExit();
                break;
        }
    }

    private static void ShowInstanceMenu()
    {
        var instances = StarterInstanceManager.ListInstances()
            .Where(instanceId => !string.Equals(
                instanceId,
                StarterInstanceManager.Current.Id,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (instances.Length == 0)
        {
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    MainMenuText("SelectInstance"),
                    MainMenuText("NoOtherInstances"),
                    LanguageManager.Ok));
            return;
        }

        DialogsManager.ShowDialog(
            null,
            new ListSelectionDialog(
                MainMenuText("SelectInstance"),
                instances,
                60f,
                item => (string)item,
                item =>
                {
                    var instanceId = (string)item;
                    ConfirmAction(
                        string.Format(MainMenuText("ConfirmSwitchInstance"), instanceId),
                        () => ExecuteApplicationCommand(
                            new SwitchInstanceCommand(instanceId)));
                }));
    }

    private static void ShowHeadlessTargetMenu()
    {
        WorldsManager.UpdateWorldsList();
        var targets = new List<HeadlessTarget>
        {
            new(null, MainMenuText("HeadlessDefaultWorld"))
        };
        targets.AddRange(WorldsManager.WorldInfos.Select(worldInfo => new HeadlessTarget(
            worldInfo.DirectoryName,
            worldInfo.WorldSettings.Name)));

        DialogsManager.ShowDialog(
            null,
            new ListSelectionDialog(
                MainMenuText("SelectHeadlessWorld"),
                targets,
                60f,
                item => ((HeadlessTarget)item).DisplayName,
                item =>
                {
                    var target = (HeadlessTarget)item;
                    var restartSession = target.WorldDirectoryName == null
                        ? null
                        : new SessionInfo
                        {
                            Target = SessionTarget.World,
                            World = target.WorldDirectoryName
                        };
                    ConfirmAction(
                        string.Format(MainMenuText("ConfirmRestartHeadless"), target.DisplayName),
                        () => ExecuteApplicationCommand(new SetRunModeCommand(
                            RunModeType.HeadlessServer,
                            restartSession)));
                }));
    }

    private static void ExecuteApplicationCommand(IGameCommand command)
    {
        var result = CommandExecutor.ExecuteApplication(command, GameManager.Project);
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

    public override void Update()
    {
        UpdateMusicButton();
        UpdateLogoAnimation();

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

        if (Children.Find<ButtonWidget>("Online")!.IsClicked)
        {
            ScreensManager.SwitchScreen("NetPlay");
        }

        var exitRequested = Input.Back && !Keyboard.BackButtonQuitsApp;
        if (exitRequested)
        {
            if (DialogsManager.HasDialogs(this) || DialogsManager.HasDialogs(ScreensManager.RootWidget))
            {
                return;
            }

            if (_mainMenuTabs.IsOpen)
            {
                _mainMenuTabs.Close();
            }
            else
            {
                ConfirmExit();
            }
        }
    }

    private void UpdateMusicButton()
    {
        if (_musicButtonClickable.IsClicked)
        {
            _isMenuMusicEnabled = !_isMenuMusicEnabled;
            MusicManager.CurrentMix = _isMenuMusicEnabled ? MusicManager.Mix.Menu : MusicManager.Mix.None;
        }

        if (_isMenuMusicEnabled && MusicManager.IsPlaying)
        {
            _musicButtonAngle += 0.4f * Time.FrameDuration;
            if (_musicButtonAngle >= 2f * MathF.PI)
            {
                _musicButtonAngle -= 2f * MathF.PI;
            }
        }

        _musicButtonIcon.RenderTransform =
            Matrix.CreateTranslation(-_musicButtonIcon.ActualSize.X / 2f, -_musicButtonIcon.ActualSize.Y / 2f, 0f) *
            Matrix.CreateRotationZ(_musicButtonAngle) *
            Matrix.CreateTranslation(_musicButtonIcon.ActualSize.X / 2f, _musicButtonIcon.ActualSize.Y / 2f, 0f);
    }

    private void UpdateLogoAnimation()
    {
        var scale = 1f + 0.02f * MathUtils.Sin(1.5f * (float)MathUtils.Remainder(Time.FrameStartTime, 10000.0));
        _logo.RenderTransform =
            Matrix.CreateTranslation(-_logo.ActualSize.X / 2f, -_logo.ActualSize.Y / 2f, 0f) *
            Matrix.CreateScale(scale, scale, 1f) *
            Matrix.CreateTranslation(_logo.ActualSize.X / 2f, _logo.ActualSize.Y / 2f, 0f);
    }

    private static void ConfirmExit()
    {
        ConfirmAction(MainMenuText("ConfirmExit"), () =>
            ExecuteApplicationCommand(new ExitApplicationCommand()));
    }

    private static void ConfirmAction(string message, Action action)
    {
        DialogsManager.Confirm(message, button =>
        {
            if (button is MessageDialogButton.Button1)
            {
                action();
            }
        });
    }

    private static string MainMenuText(string key) =>
        LanguageManager.GetContentWidgets("MainMenuScreen", key);

    private enum PowerMenuAction
    {
        Restart,
        SwitchInstance,
        RestartHeadless,
        Exit
    }

    private sealed record HeadlessTarget(
        string? WorldDirectoryName,
        string DisplayName);
}
