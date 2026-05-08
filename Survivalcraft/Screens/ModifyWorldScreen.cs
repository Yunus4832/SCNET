using System.Globalization;
using System.Xml.Linq;
using EntitySystem.TemplatesDatabase;

namespace Game.Screens;

public class ModifyWorldScreen : Screen
{
    private const string _typeName = "ModifyWorldScreen";

    // 新增一个布尔变量，用于控制是否允许在残酷模式下更改游戏模式
    public bool AllowGameModeChangeInCruelMode = false; // 默认值为 false

    private readonly ButtonWidget _applyButton;

    private bool _changingGameModeAllowed;

    //其他模式能否改残酷模式
    private bool _cruelAllowed = true;

    private readonly ValuesDictionary _currentWorldSettingsData = new();

    private readonly ButtonWidget _deleteButton;

    private readonly LabelWidget _descriptionLabel;

    private string _directoryName = string.Empty;

    private readonly LabelWidget _errorLabel;

    private readonly ButtonWidget _gameModeButton;

    private readonly TextBoxWidget _nameTextBox;

    private readonly ValuesDictionary _originalWorldSettingsData = new();

    private readonly LabelWidget _seedLabel;

    private readonly ButtonWidget _uploadButton;

    private readonly ButtonWidget _worldOptionsButton;

    private WorldSettings WorldSettings
    {
        get => field is not null ? field : throw new InvalidOperationException("WorldSetting is not initializedje");
        set;
    }

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

    public ModifyWorldScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ModifyWorldScreen");
        LoadContents(this, node);
        _nameTextBox = Children.Find<TextBoxWidget>("Name")!;
        _daySpeedTextBox = Children.Find<TextBoxWidget>("DaySpeed")!;
        _seedLabel = Children.Find<LabelWidget>("Seed")!;
        _gameModeButton = Children.Find<ButtonWidget>("GameMode")!;
        _worldOptionsButton = Children.Find<ButtonWidget>("WorldOptions")!;
        _errorLabel = Children.Find<LabelWidget>("Error")!;
        _descriptionLabel = Children.Find<LabelWidget>("Description")!;
        _applyButton = Children.Find<ButtonWidget>("Apply")!;
        _deleteButton = Children.Find<ButtonWidget>("Delete")!;
        _uploadButton = Children.Find<ButtonWidget>("Upload")!;

        _maxPlayerConfigPanelWidget = Children.Find<UniformSpacingPanelWidget>("MaxPlayerConfig")!;
        _serverConfigPanelWidget = Children.Find<UniformSpacingPanelWidget>("ServerConfig")!;
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

        _disableBlocks.MaximumLength = int.MaxValue;
        _nameTextBox.TextChanged += delegate { WorldSettings.Name = _nameTextBox.Text; };
    }

    public override void Enter(object[] parameters)
    {
        if (ScreensManager.PreviousScreen == null ||
            ScreensManager.PreviousScreen.GetType() == typeof(WorldOptionsScreen))
        {
            return;
        }

        _directoryName = (string)parameters[0];
        WorldSettings = (WorldSettings)parameters[1];
        _originalWorldSettingsData.Clear();
        _maxPlayer.Text = WorldSettings.MaxOnlinePlayerCount.ToString();
        _runServer.IsChecked = WorldSettings.RunServer;
        _needLogin.IsChecked = WorldSettings.IsNeedCommunityLogin;
        _randomSpawPostion.IsChecked = WorldSettings.RandomSpawnPosition;
        _daySpeedTextBox.Text = WorldSettings.DaySpeed.ToString(CultureInfo.InvariantCulture);
        _recoverySpeed.Text = WorldSettings.RecoverFactor.ToString(CultureInfo.InvariantCulture);
        _disableBlocks.Text = WorldSettings.DisableBlocks;
        WorldSettings.Save(_originalWorldSettingsData, true);
        // 修改逻辑：根据 allowGameModeChangeInCruelMode 的值来决定是否允许更改游戏模式
        _changingGameModeAllowed = AllowGameModeChangeInCruelMode || WorldSettings.GameMode != GameMode.Cruel;
        _keywordBlocking.Text = WorldSettings.KeywordBlocking;
    }

    public override void Update()
    {
        _serverConfigPanelWidget.IsVisible = _runServer.IsChecked;
        _maxPlayerConfigPanelWidget.IsVisible = _runServer.IsChecked;
        _loginConfigPanelWidget.IsVisible = _runServer.IsChecked;
        _daySpeedConfig.IsVisible = _runServer.IsChecked;
        _recoverySpeedConfig.IsVisible = _runServer.IsChecked;
        _disableBlocksPanel.IsVisible = _runServer.IsChecked;
        _randomSpawnPostionPanel.IsVisible = _runServer.IsChecked;
        _keywordBlockingConfigPanel.IsVisible = _runServer.IsChecked;
        _keywordBlocking.IsVisible = _runServer.IsChecked;
        WorldSettings.KeywordBlocking = _keywordBlocking.Text;

        int.TryParse(_maxPlayer.Text, out var reslut);
        WorldSettings.MaxOnlinePlayerCount = (ushort)MathUtils.Max(reslut, 1);
        WorldSettings.DisableBlocks = _disableBlocks.Text;

        if (_gameModeButton.IsClicked && _changingGameModeAllowed)
        {
            DialogsManager.ShowDialog(null,
                new SelectGameModeDialog(string.Empty, _cruelAllowed,
                    delegate(GameMode gameMode) { WorldSettings.GameMode = gameMode; }));
        }

        WorldSettings.RunServer = _runServer.IsChecked;
        WorldSettings.IsNeedCommunityLogin = _needLogin.IsChecked;
        WorldSettings.Password = _password.Text;
        WorldSettings.RandomSpawnPosition = _randomSpawPostion.IsChecked;

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

        WorldSettings.DaySpeed = daySpeed;
        WorldSettings.RecoverFactor = recoverSpeed;
        _currentWorldSettingsData.Clear();
        WorldSettings.Save(_currentWorldSettingsData, true);
        var flag = !CompareValueDictionaries(_originalWorldSettingsData, _currentWorldSettingsData);
        var flag2 = WorldsManager.ValidateWorldName(WorldSettings.Name);
        _nameTextBox.Text = WorldSettings.Name;
        _seedLabel.Text = WorldSettings.Seed;
        _gameModeButton.Text = LanguageControl.Get("GameMode", WorldSettings.GameMode.ToString());
        _gameModeButton.IsEnabled = _changingGameModeAllowed;
        _errorLabel.IsVisible = !flag2;
        _descriptionLabel.IsVisible = flag2;
        _uploadButton.IsEnabled = flag2 && !flag;
        _applyButton.IsEnabled = flag2 && flag;
        _descriptionLabel.Text = StringsManager.GetString("GameMode." + WorldSettings.GameMode + ".Description");
        if (_worldOptionsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("WorldOptions", WorldSettings, true);
        }

        if (_deleteButton.IsClicked)
        {
            var dialog = new MessageDialog(
                LanguageControl.Get(_typeName, 1),
                LanguageControl.Get(_typeName, 2),
                LanguageControl.Get("Usual", "yes"),
                LanguageControl.Get("Usual", "no"),
                new Vector2(-1f),
                (button, self) =>
                {
                    if (button == MessageDialogButton.Button1)
                    {
                        WorldsManager.DeleteWorld(_directoryName);
                        ScreensManager.SwitchScreen("Play");
                    }

                    DialogsManager.HideDialog(self);
                })
            {
                AutoHide = false
            };
            DialogsManager.ShowDialog(null, dialog);
        }

        if (_uploadButton.IsClicked && flag2 && !flag)
        {
            ExternalContentManager.ShowUploadUi(ExternalContentType.World, _directoryName);
        }

        if ((_applyButton.IsClicked && flag2) & flag)
        {
            if (WorldSettings.GameMode != 0 && WorldSettings.GameMode != GameMode.Adventure)
            {
                WorldSettings.ResetOptionsForNonCreativeMode();
            }

            WorldsManager.ChangeWorld(_directoryName, WorldSettings);
            ScreensManager.SwitchScreen("Play");
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            if (flag)
            {
                DialogsManager.ShowDialog(null, new MessageDialog(LanguageControl.Get(_typeName, 3),
                    LanguageControl.Get(_typeName, 4), LanguageControl.Get("Usual", "yes"),
                    LanguageControl.Get("Usual", "no"), delegate(MessageDialogButton button)
                    {
                        if (button == MessageDialogButton.Button1)
                        {
                            ScreensManager.SwitchScreen("Play");
                        }
                    }));
            }
            else
            {
                ScreensManager.SwitchScreen("Play");
            }
        }
    }

    private static bool CompareValueDictionaries(ValuesDictionary d1, ValuesDictionary d2)
    {
        if (d1.Count != d2.Count)
        {
            return false;
        }

        foreach (var item in d1)
        {
            var value = d2.GetValue<object>(item.Key, false);
            if (value is ValuesDictionary valuesDictionary)
            {
                if (item.Value is not ValuesDictionary valuesDictionary2 ||
                    !CompareValueDictionaries(valuesDictionary, valuesDictionary2))
                {
                    return false;
                }
            }
            else if (!Equals(value, item.Value))
            {
                return false;
            }
        }

        return true;
    }
}
