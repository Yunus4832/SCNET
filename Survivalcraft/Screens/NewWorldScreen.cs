using System.Xml.Linq;
using Game.NetWork;

namespace Game.Screens;

public class NewWorldScreen : Screen
{
    private const string _typeName = "NewWorldScreen";

    private readonly LabelWidget _blankSeedLabel;

    private readonly LabelWidget _descriptionLabel;

    private readonly LabelWidget _errorLabel;

    private readonly ButtonWidget _gameModeButton;

    private readonly TextBoxWidget _nameTextBox;

    private readonly ButtonWidget _playButton;

    private readonly Random _random = new();

    private readonly TextBoxWidget _seedTextBox;

    private readonly ButtonWidget _startingPositionButton;

    private readonly ButtonWidget _worldOptionsButton;

    private WorldSettings _worldSettings = null!;

    private readonly TextBoxWidget _password;

    private readonly TextBoxWidget _maxPlayer;

    private readonly TextBoxWidget _daySpeedTextBox;

    private readonly TextBoxWidget _disableBlocks;

    private readonly TextBoxWidget _recoverySpeed;

    private readonly TextBoxWidget _keywordBlocking;

    private readonly CheckboxWidget _runServer;

    private readonly CheckboxWidget _needLogin;

    private readonly CheckboxWidget _randomSpawPostion;

    private readonly UniformSpacingPanelWidget _serverConfigPanelWidget;

    private readonly UniformSpacingPanelWidget _maxPlayerConfigPanelWidget;

    private readonly UniformSpacingPanelWidget _loginConfigPanelWidget;

    private readonly UniformSpacingPanelWidget _daySpeedConfig;

    private readonly UniformSpacingPanelWidget _disableBlocksPanel;

    private readonly UniformSpacingPanelWidget _recoverySpeedConfig;

    private readonly UniformSpacingPanelWidget _randomSpawnPostionPanel;

    private readonly UniformSpacingPanelWidget _keywordBlockingConfigPanel;

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
        _descriptionLabel = Children.Find<LabelWidget>("Description")!;
        _errorLabel = Children.Find<LabelWidget>("Error")!;
        _playButton = Children.Find<ButtonWidget>("Play")!;
        _serverConfigPanelWidget = Children.Find<UniformSpacingPanelWidget>("ServerConfig")!;
        _maxPlayerConfigPanelWidget = Children.Find<UniformSpacingPanelWidget>("MaxPlayerConfig")!;
        _loginConfigPanelWidget = Children.Find<UniformSpacingPanelWidget>("LoginConfig")!;
        _daySpeedConfig = Children.Find<UniformSpacingPanelWidget>("DaySpeedConfig")!;
        _recoverySpeedConfig = Children.Find<UniformSpacingPanelWidget>("RecoverySpeedConfig")!;
        _disableBlocksPanel = Children.Find<UniformSpacingPanelWidget>("DisableBlocksPanel")!;
        _randomSpawnPostionPanel = Children.Find<UniformSpacingPanelWidget>("RandomSpawnPositionPanel")!;
        _keywordBlockingConfigPanel = Children.Find<UniformSpacingPanelWidget>("KeywordBlockingConfig")!;
        _runServer = Children.Find<CheckboxWidget>("RunServer")!;
        _needLogin = Children.Find<CheckboxWidget>("NeedLogin")!;
        _randomSpawPostion = Children.Find<CheckboxWidget>("RandomSpawnPosition")!;
        _password = Children.Find<TextBoxWidget>("Password")!;
        _maxPlayer = Children.Find<TextBoxWidget>("MaxPlayers")!;
        _disableBlocks = Children.Find<TextBoxWidget>("DisableBlocks")!;
        _daySpeedTextBox = Children.Find<TextBoxWidget>("DaySpeed")!;
        _recoverySpeed = Children.Find<TextBoxWidget>("RecoverySpeed")!;
        _keywordBlocking = Children.Find<TextBoxWidget>("KeywordBlocking")!;

        _daySpeedTextBox.Text = "1.0";

        _recoverySpeed.Text = "1.0";

        _disableBlocks.MaximumLength = int.MaxValue;

        _nameTextBox.TextChanged += delegate { _worldSettings.Name = _nameTextBox.Text; };
        _seedTextBox.TextChanged += delegate { _worldSettings.Seed = _seedTextBox.Text; };
    }

    public override void Enter(object[] parameters)
    {
        if (ScreensManager.PreviousScreen?.GetType() != typeof(WorldOptionsScreen))
        {
            _worldSettings = new WorldSettings
            {
                Name = WorldsManager.NewWorldNames[_random.Int(0, WorldsManager.NewWorldNames.Count - 1)],
                OriginalSerializationVersion = VersionsManager.SerializationVersion
            };
        }

        _runServer.IsChecked = _worldSettings.RunServer;
        _needLogin.IsChecked = _worldSettings.IsNeedCommunityLogin;
        _randomSpawPostion.IsChecked = _worldSettings.RandomSpawnPosition;
    }

    public override void Update()
    {
        _worldSettings.RunServer = _runServer.IsChecked;
        _serverConfigPanelWidget.IsVisible = _worldSettings.RunServer;
        _maxPlayerConfigPanelWidget.IsVisible = _worldSettings.RunServer;
        _loginConfigPanelWidget.IsVisible = _worldSettings.RunServer;
        _daySpeedConfig.IsVisible = _worldSettings.RunServer;
        _recoverySpeedConfig.IsVisible = _worldSettings.RunServer;
        _disableBlocksPanel.IsVisible = _worldSettings.RunServer;
        _randomSpawnPostionPanel.IsVisible = _worldSettings.RunServer;
        _keywordBlockingConfigPanel.IsVisible = _worldSettings.RunServer;
        _keywordBlocking.IsVisible = _worldSettings.RunServer;

        int.TryParse(_maxPlayer.Text, out var result);
        _worldSettings.MaxOnlinePlayerCount = (ushort)MathUtils.Max(result, 1);
        _worldSettings.DisableBlocks = _disableBlocks.Text;
        _worldSettings.IsNeedCommunityLogin = _needLogin.IsChecked;
        _worldSettings.Password = _password.Text;
        _worldSettings.KeywordBlocking = _keywordBlocking.Text;
        float.TryParse(_daySpeedTextBox.Text, out var daySpeed);
        float.TryParse(_recoverySpeed.Text, out var recoverSpeed);
        if (daySpeed <= 0f || daySpeed > 1f)
        {
            daySpeed = 1f;
        }

        if (recoverSpeed <= 0f)
        {
            recoverSpeed = 1f;
        }

        _worldSettings.DaySpeed = daySpeed;
        _worldSettings.RecoverFactor = recoverSpeed;
        _worldSettings.RandomSpawnPosition = _randomSpawPostion.IsChecked;
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
        _gameModeButton.Text = LanguageControl.Get("GameMode", _worldSettings.GameMode.ToString());
        _startingPositionButton.Text =
            LanguageControl.Get("StartingPositionMode", _worldSettings.StartingPositionMode.ToString());
        _playButton.IsVisible = flag;
        _errorLabel.IsVisible = !flag;
        _blankSeedLabel.IsVisible = _worldSettings.Seed.Length == 0 && !_seedTextBox.HasFocus;
        _descriptionLabel.Text = StringsManager.GetString("GameMode." + _worldSettings.GameMode + ".Description");
        if (_worldOptionsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("WorldOptions", _worldSettings, false);
        }

        if (_playButton.IsClicked && WorldsManager.ValidateWorldName(_nameTextBox.Text))
        {
            var worldInfo = WorldsManager.CreateWorld(_worldSettings);
            if (_worldSettings.GameMode != GameMode.Creative)
            {
                _worldSettings.ResetOptionsForNonCreativeMode();
            }

            if (_runServer.IsChecked)
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

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen("Play");
        }
    }
}
