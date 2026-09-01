using System.Xml.Linq;

using Game.Network;

namespace Game.Screens;

public class NewWorldScreen : Screen
{
    private const string _typeName = nameof(NewWorldScreen);

    private readonly LabelWidget _blankSeedLabel;

    private readonly LabelWidget _errorLabel;

    private readonly ButtonWidget _gameModeButton;

    private readonly TextBoxWidget _nameTextBox;

    private readonly ButtonWidget _playButton;

    private readonly Random _random = new();

    private readonly TextBoxWidget _seedTextBox;

    private readonly ButtonWidget _serverSettingsButton;

    private readonly ButtonWidget _startingPositionButton;

    private readonly ButtonWidget _worldOptionsButton;

    private WorldSettings _worldSettings = null!;

    public NewWorldScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/NewWorldScreen");
        LoadContents(this, node);
        _nameTextBox = Children.Find<TextBoxWidget>("Name")!;
        _seedTextBox = Children.Find<TextBoxWidget>("Seed")!;
        _gameModeButton = Children.Find<ButtonWidget>("GameMode")!;
        _startingPositionButton = Children.Find<ButtonWidget>("StartingPosition")!;
        _worldOptionsButton = Children.Find<ButtonWidget>("WorldOptions")!;
        _blankSeedLabel = Children.Find<LabelWidget>("BlankSeed")!;
        _errorLabel = Children.Find<LabelWidget>("Error")!;
        _playButton = Children.Find<ButtonWidget>("Play")!;
        _serverSettingsButton = Children.Find<ButtonWidget>("ServerSettings")!;

        _nameTextBox.TextChanged += delegate { _worldSettings.Name = _nameTextBox.Text; };
        _seedTextBox.TextChanged += delegate { _worldSettings.Seed = _seedTextBox.Text; };
    }

    public override void Enter(object[] parameters)
    {
        if (parameters.Length > 0 && parameters[0] is WorldSettings worldSettings)
        {
            _worldSettings = worldSettings;
        }
        else if (ScreensManager.PreviousScreen?.GetType() != typeof(WorldOptionsScreen))
        {
            _worldSettings = new WorldSettings
            {
                Name = WorldsManager.NewWorldNames[_random.Int(0, WorldsManager.NewWorldNames.Count - 1)]
            };
        }
    }

    public override void Update()
    {
        if (_gameModeButton.IsClicked)
        {
            DialogsManager.ShowDialog(null,
                new SelectGameModeDialog(string.Empty, false,
                    delegate(GameMode gameMode) { _worldSettings.GameMode = gameMode; }));
        }

        if (_startingPositionButton.IsClicked)
        {
            var enumValues2 = EnumUtils.GetEnumValues(typeof(StartingPositionMode));
            _worldSettings.StartingPositionMode =
                (StartingPositionMode)((enumValues2.IndexOf((int)_worldSettings.StartingPositionMode) + 1) %
                                       enumValues2.Count);
        }

        var flag = WorldsManager.ValidateWorldName(_worldSettings.Name);
        _nameTextBox.Text = _worldSettings.Name;
        _seedTextBox.Text = _worldSettings.Seed;
        _gameModeButton.Text = LanguageManager.Get("GameMode", _worldSettings.GameMode.ToString());
        _startingPositionButton.Text =
            LanguageManager.Get("StartingPositionMode", _worldSettings.StartingPositionMode.ToString());
        _playButton.IsVisible = flag;
        _errorLabel.IsVisible = !flag;
        _blankSeedLabel.IsVisible = _worldSettings.Seed.Length == 0 && !_seedTextBox.HasFocus;
        if (_worldOptionsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("WorldOptions", _worldSettings, false);
        }

        if (_serverSettingsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("WorldServerSettings", _worldSettings, "NewWorld", _worldSettings);
        }

        if (_playButton.IsClicked && WorldsManager.ValidateWorldName(_nameTextBox.Text))
        {
            var worldInfo = WorldsManager.CreateWorld(_worldSettings);
            if (_worldSettings.GameMode != GameMode.Creative)
            {
                _worldSettings.ResetOptionsForNonCreativeMode();
            }

            if (_worldSettings.RunServer)
            {
                PrepareWorldModsAndPlay(worldInfo, runServer: true);
            }
            else
            {
                PrepareWorldModsAndPlay(worldInfo, runServer: false);
            }
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Play");
        }
    }

    private void PrepareWorldModsAndPlay(WorldInfo worldInfo, bool runServer)
    {
        var busyDialog = new BusyDialog("准备世界模组", "正在检查所需模组...");
        DialogsManager.ShowDialog(null, busyDialog);
        Task.Run(() =>
        {
            try
            {
                var result = ModRestartHelper.PrepareWorldSession(
                    worldInfo,
                    message => Dispatcher.Dispatch(() => busyDialog.SmallMessage = message));
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    if (!result.RequiresRestart)
                    {
                        PlayPreparedWorld(worldInfo, runServer);
                        return;
                    }

                    ConfirmWorldModRestart(result);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Dispatch(() =>
                {
                    DialogsManager.HideDialog(busyDialog);
                    DialogsManager.Alert(
                        "模组准备失败",
                        $"无法准备该世界需要的模组。\n{ex.Message}");
                });
            }
        });
    }

    private static void ConfirmWorldModRestart(RemoteModSessionPreparation result)
    {
        DialogsManager.ShowDialog(
            null,
            new MessageDialog(
                "需要重启游戏",
                $"{result.RestartReason}\n\n是否现在重启？",
                "重启",
                "取消",
                button =>
                {
                    if (button != MessageDialogButton.Button1)
                    {
                        return;
                    }

                    GameExitManager.RequestRestart(result.RemoteSession!, result.SessionProfile!);
                }));
    }

    private void PlayPreparedWorld(WorldInfo worldInfo, bool runServer)
    {
        if (runServer)
        {
            if (CommonLib.StartServer())
            {
                ScreensManager.SwitchScreen("GameLoading", worldInfo, string.Empty);
            }
            else
            {
                DialogsManager.ShowDialog(
                    this,
                    new MessageDialog(
                        "提示",
                        "创建服务器失败，端口已被占用",
                        "确定", string.Empty,
                        _ =>
                        {
                            CommonLib.Net.StopImmediate();
                            DialogsManager.HideAllDialogs();
                        }));
            }
        }
        else
        {
            ScreensManager.SwitchScreen("GameLoading", worldInfo, string.Empty);
        }
    }
}
