using System.Xml.Linq;

using Engine.Input;

using Game.Application;
using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Network;

namespace Game.Screens;

public class MainMenuScreen : Screen
{
    private readonly ExpandableButtonWidget _powerButton;

    private readonly ExpandableButtonWidget _languageButton;

    private readonly CanvasWidget _logo;

    private readonly ClickableWidget _logoClickable;

    private readonly RectangleWidget _musicButtonIcon;

    private readonly ClickableWidget _musicButtonClickable;

    private readonly CanvasWidget _announcementTicker;

    private readonly LabelWidget _announcementTickerText;

    private readonly ClickableWidget _announcementTickerClickable;

    private readonly ClickableWidget _versionClickable;

    private IReadOnlyList<string> _announcementTitles = [];

    private double _tickerStartedAt;

    private int _tickerIndex;

    private bool _isMenuMusicEnabled = true;

    private float _musicButtonAngle;

    private static readonly string _versionString = $"Version {VersionsManager.Version}";

    public MainMenuScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/MainMenuScreen");
        LoadContents(this, node);
        _powerButton = Children.Find<ExpandableButtonWidget>("PowerButton")!;
        _languageButton = Children.Find<ExpandableButtonWidget>("LanguageButton")!;
        _logo = Children.Find<CanvasWidget>("Logo")!;
        _logoClickable = Children.Find<ClickableWidget>("Logo.Clickable")!;
        _musicButtonIcon = Children.Find<RectangleWidget>("MusicButton.Icon")!;
        _musicButtonClickable = Children.Find<ClickableWidget>("MusicButton.Clickable")!;
        _announcementTicker = Children.Find<CanvasWidget>("AnnouncementTicker")!;
        _announcementTickerText = Children.Find<LabelWidget>("AnnouncementTicker.Text")!;
        _announcementTickerClickable = Children.Find<ClickableWidget>("AnnouncementTicker.Clickable")!;
        _versionClickable = Children.Find<ClickableWidget>("Version.Clickable")!;
        _announcementTickerText.Ellipsis = true;
        _announcementTickerText.MaxLines = 1;
        Children.Find<LabelWidget>("Version")!.Text = _versionString;

        ConfigureMenuButtons();
    }

    public override void Enter(object[] parameters)
    {
        UpdateAnnouncementTicker();
        GameInformationManager.AnnouncementsChanged += UpdateAnnouncementTicker;
        MusicManager.CurrentMix = _isMenuMusicEnabled ? MusicManager.Mix.Menu : MusicManager.Mix.None;

        // 如果当前已连接网络，则停止连接
        if (CommonLib.Net.CurrentStage == NetNode.Stage.Connected)
        {
            CommonLib.Net.Stop();
        }
    }

    public override void Leave()
    {
        _powerButton.Close();
        _languageButton.Close();
        GameInformationManager.AnnouncementsChanged -= UpdateAnnouncementTicker;
        Keyboard.BackButtonQuitsApp = false;
    }

    private void UpdateAnnouncementTicker()
    {
        _announcementTitles = GameInformationManager.AnnouncementTitles;
        _announcementTicker.IsVisible = _announcementTitles.Count > 0;
        _tickerIndex = 0;
        _tickerStartedAt = Time.RealTime;
        _announcementTickerText.Text = _announcementTitles.Count > 0
            ? _announcementTitles[0]
            : string.Empty;
    }

    private void UpdateAnnouncementTickerAnimation()
    {
        if (_announcementTitles.Count == 0)
        {
            return;
        }

        var elapsed = Time.RealTime - _tickerStartedAt;
        if (elapsed >= 8.0)
        {
            _tickerIndex = (_tickerIndex + 1) % _announcementTitles.Count;
            _tickerStartedAt = Time.RealTime;
            _announcementTickerText.Text = _announcementTitles[_tickerIndex];
            elapsed = 0.0;
        }

        var width = _announcementTicker.ActualSize.X;
        var offset = elapsed < 0.5 ? width * (1f - (float)(elapsed / 0.5))
            : elapsed > 7.5 ? -width * (float)((elapsed - 7.5) / 0.5)
            : 0f;
        _announcementTicker.SetWidgetPosition(_announcementTickerText, new Vector2(offset, 0f));
    }

    private void ConfigureMenuButtons()
    {
        _powerButton.Items = () =>
        [
            new ExpandableButtonItem(MainMenuText("Restart"), () => ExecutePowerMenuAction(PowerMenuAction.Restart)),
            new ExpandableButtonItem(
                MainMenuText("SwitchInstance"),
                () => ExecutePowerMenuAction(PowerMenuAction.SwitchInstance)
            ),
            new ExpandableButtonItem(MainMenuText("Exit"), () => ExecutePowerMenuAction(PowerMenuAction.Exit))
        ];
        _languageButton.Items = () => Localization.ClientLanguage.Catalog.LanguageTypes
            .Select(languageType => new ExpandableButtonItem(
                    Localization.ClientLanguage.Catalog.GetLanguageDisplayName(languageType),
                    () => SettingsUiScreen.ChangeLanguage(languageType)
                )
            )
            .ToArray();
    }

    private static void ExecutePowerMenuAction(PowerMenuAction action)
    {
        switch (action)
        {
            case PowerMenuAction.Restart:
                ConfirmAction(
                    MainMenuText("ConfirmRestart"),
                    () =>
                    {
                        var session = SessionInfoManager.CaptureCurrentSession();
                        session.Target = SessionTarget.MainMenu;
                        ExecuteApplicationCommand(new RestartApplicationCommand(session));
                    }
                );
                break;
            case PowerMenuAction.SwitchInstance:
                ShowInstanceMenu();
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
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .ToArray();
        if (instances.Length == 0)
        {
            DialogsManager.ShowDialog(
                null,
                new MessageDialog(
                    MainMenuText("SelectInstance"),
                    MainMenuText("NoOtherInstances"),
                    Localization.ClientLanguage.Catalog.Ok
                )
            );
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
                        () => ExecuteApplicationCommand(new SwitchInstanceCommand(instanceId))
                    );
                }
            )
        );
    }


    private static void ExecuteApplicationCommand(IGameCommand command)
    {
        var result = CommandExecutor.ExecuteLocal(
            CurrentModRuntime.Value?.Commands,
            command,
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

    public override void Update()
    {
        if (Children.Find<ButtonWidget>("IdentityButton")!.IsClicked)
        {
            _powerButton.Close();
            _languageButton.Close();
            ScreensManager.SwitchScreen("ManageIdentities");
        }

        UpdateMusicButton();
        UpdateLogoAnimation();
        UpdateAnnouncementTickerAnimation();

        if (_logoClickable.IsClicked)
        {
            ScreensManager.SwitchScreen("InstanceManagement");
        }

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

        if (_announcementTickerClickable.IsClicked)
        {
            GameInformationManager.ShowAnnouncements();
        }

        if (_versionClickable.IsClicked)
        {
            GameInformationManager.CheckManually();
        }

        var exitRequested = Input.Back && !Keyboard.BackButtonQuitsApp;
        if (exitRequested)
        {
            if (DialogsManager.HasDialogs(this) || DialogsManager.HasDialogs(ScreensManager.RootWidget))
            {
                return;
            }

            if (_powerButton.IsOpen || _languageButton.IsOpen)
            {
                _powerButton.Close();
                _languageButton.Close();
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
        ConfirmAction(
            MainMenuText("ConfirmExit"),
            () =>
                ExecuteApplicationCommand(new ExitApplicationCommand())
        );
    }

    private static void ConfirmAction(string message, Action action)
    {
        DialogsManager.Confirm(
            message,
            button =>
            {
                if (button is MessageDialogButton.Button1)
                {
                    action();
                }
            }
        );
    }

    private static string MainMenuText(string key) =>
        Localization.ClientLanguage.Catalog.GetContentWidgets("MainMenuScreen", key);

    private enum PowerMenuAction
    {
        Restart = 0,

        SwitchInstance = 1,


        Exit = 3
    }
}
