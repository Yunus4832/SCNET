using System.Xml.Linq;

using Engine.Input;

using EntitySystem.Core;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Packages;

namespace Game.Screens;

public class PlayerScreen : Screen
{
    public enum Mode
    {
        Initial,
        Add,
        Edit
    }

    private const string _typeName = "PlayerScreen";

    private readonly BusyDialog _fetchingPlayerDialog = new("下载玩家...", string.Empty);

    public double EnterTime;

    private readonly ButtonWidget _addAnotherButton;

    private readonly ButtonWidget _addButton;

    private readonly ButtonWidget _characterSkinButton;

    private readonly LabelWidget _characterSkinLabel;

    private readonly CharacterSkinsCache _characterSkinsCache;

    private readonly ButtonWidget _controlsButton;

    private readonly LabelWidget _controlsLabel;

    private readonly ButtonWidget _deleteButton;

    private readonly LabelWidget _descriptionLabel;

    private readonly WidgetInputDevice[] _inputDevices =
    [
        WidgetInputDevice.None,
        WidgetInputDevice.GamePad1,
        WidgetInputDevice.GamePad2,
        WidgetInputDevice.GamePad3,
        WidgetInputDevice.GamePad4
    ];

    private Mode _mode;

    private readonly TextBoxWidget _nameTextBox;

    private bool _nameWasInvalid;

    private readonly ButtonWidget _playButton;

    private readonly ButtonWidget _playerClassButton;

    private PlayerData _playerData = null!;

    private readonly PlayerModelWidget _playerModel;

    public PlayerScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/PlayerScreen");
        LoadContents(this, node);
        _playerModel = Children.Find<PlayerModelWidget>("Model")!;
        _playerClassButton = Children.Find<ButtonWidget>("PlayerClassButton")!;
        _nameTextBox = Children.Find<TextBoxWidget>("Name")!;
        _characterSkinLabel = Children.Find<LabelWidget>("CharacterSkinLabel")!;
        _characterSkinButton = Children.Find<ButtonWidget>("CharacterSkinButton")!;
        _controlsLabel = Children.Find<LabelWidget>("ControlsLabel")!;
        _controlsButton = Children.Find<ButtonWidget>("ControlsButton")!;
        _descriptionLabel = Children.Find<LabelWidget>("DescriptionLabel")!;
        _addButton = Children.Find<ButtonWidget>("AddButton")!;
        _addAnotherButton = Children.Find<ButtonWidget>("AddAnotherButton")!;
        _deleteButton = Children.Find<ButtonWidget>("DeleteButton")!;
        _playButton = Children.Find<ButtonWidget>("PlayButton")!;
        _characterSkinsCache = new CharacterSkinsCache();
        _playerModel.CharacterSkinsCache = _characterSkinsCache;
        _nameTextBox.FocusLost += delegate
        {
            if (VerifyName())
            {
                _playerData.Name = _nameTextBox.Text.Trim();
            }
            else
            {
                _nameWasInvalid = true;
            }
        };
    }

    public override void Enter(object[] parameters)
    {
        EnterTime = Time.RealTime;
        _mode = (Mode)parameters[0];
        _playerData = _mode == Mode.Edit ? (PlayerData)parameters[1] : new PlayerData((Project)parameters[1]);

        if (_mode == Mode.Initial)
        {
            _playerClassButton.IsEnabled = true;
            _addButton.IsVisible = false;
            _deleteButton.IsVisible = false;
            _playButton.IsVisible = true;
            _addAnotherButton.IsVisible = _playerData.SubsystemPlayers.PlayersData.Count < 3;
        }
        else if (_mode == Mode.Add)
        {
            _playerClassButton.IsEnabled = true;
            _addButton.IsVisible = true;
            _deleteButton.IsVisible = false;
            _playButton.IsVisible = false;
            _addAnotherButton.IsVisible = false;
        }
        else if (_mode == Mode.Edit)
        {
            _playerClassButton.IsEnabled = false;
            _addButton.IsVisible = false;
            _deleteButton.IsVisible = _playerData.SubsystemPlayers.PlayersData.Count > 1;
            _playButton.IsVisible = false;
            _addAnotherButton.IsVisible = false;
        }

        _addAnotherButton.IsVisible &= CommonLib.WorkType == WorkType.Local;
        _deleteButton.IsVisible &= CommonLib.WorkType != WorkType.Client;
    }

    public override void Leave()
    {
        _characterSkinsCache.Clear();
        _playerData = null!;
    }

    public override void Update()
    {
        if (GameManager.Project is null)
        {
            throw new InvalidOperationException("GameManager.Project is not initialized");
        }

        if (_mode == Mode.Edit)
        {
            GameManager.UpdateProject();
        }

        if (Time.RealTime - EnterTime > 120 && CommonLib.WorkType == WorkType.Client &&
            CommonLib.Net.NetManager.IsRunning)
            //客户端卡在新增玩家界面两分钟自动中断连接
        {
            CommonLib.Net.Stop("客户端主动关闭连接");
        }

        if (DialogsManager.ReadOnlyDialogs.Contains(_fetchingPlayerDialog))
        {
            if (_playerData.SubsystemPlayers.FindPlayerData(p => p.IsMainPlayer) == null)
            {
                return;
            }

            DialogsManager.HideAllDialogs();
            ScreensManager.SwitchScreen("Game", WorkType.Client);

            return;
        }

        _characterSkinsCache.GetTexture(_playerData.CharacterSkinName);
        _playerModel.PlayerClass = _playerData.PlayerClass;
        _playerModel.CharacterSkinName = _playerData.CharacterSkinName;
        _playerClassButton.Text = _playerData.PlayerClass.ToString();
        if (!_nameTextBox.HasFocus)
        {
            _nameTextBox.Text = _playerData.Name;
        }

        var gameInfo = GameManager.Project.FindSubsystem<SubsystemGameInfo>();
        if (gameInfo is { WorldSettings.IsNeedCommunityLogin: true })
        {
            if (!string.IsNullOrEmpty(SettingsManager.CommunityNickName))
            {
                _nameTextBox.Text = SettingsManager.CommunityNickName;
                _nameTextBox.IsEnabled = false;
            }
        }

        _characterSkinLabel.Text = CharacterSkinsManager.GetDisplayName(_playerData.CharacterSkinName);
        _controlsLabel.Text =
            GetDeviceDisplayName(_inputDevices.FirstOrDefault(id => (id & _playerData.InputDevice) != 0));
        var valuesDictionary = DatabaseManager.FindValuesDictionaryForComponent(
            DatabaseManager.FindEntityValuesDictionary(_playerData.GetEntityTemplateName(), true)!,
            typeof(ComponentCreature)
        );
        if (valuesDictionary != null)
        {
            var dy = valuesDictionary.GetValue<string>("Description");
            if (dy.StartsWith('[') && dy.EndsWith(']'))
            {
                var lp = dy.Substring(1, dy.Length - 2).Split([":"], StringSplitOptions.RemoveEmptyEntries);
                dy = LanguageControl.GetDatabase("Description", lp[1]);
            }

            _descriptionLabel.Text = dy;
        }

        if (_playerClassButton.IsClicked)
        {
            _playerData.PlayerClass =
                _playerData.PlayerClass == PlayerClass.Male ? PlayerClass.Female : PlayerClass.Male;
            _playerData.RandomizeCharacterSkin();
            if (_playerData.IsDefaultName)
            {
                _playerData.ResetName();
            }
        }

        if (_characterSkinButton.IsClicked)
        {
            CharacterSkinsManager.UpdateCharacterSkinsList();
            var items = CharacterSkinsManager.ReadOnlyCharacterSkinsNames.Where(n =>
                CharacterSkinsManager.GetPlayerClass(n) == _playerData.PlayerClass ||
                !CharacterSkinsManager.GetPlayerClass(n).HasValue);
            var dialog = new ListSelectionDialog(
                LanguageControl.Get(_typeName, 1),
                items, 64f,
                delegate(object item)
                {
                    var node = ContentManager.Get<XElement>("Widgets/CharacterSkinItem");
                    var obj = (ContainerWidget)LoadWidget(this, node, null);
                    var texture = _characterSkinsCache.GetTexture((string)item);
                    obj.Children.Find<LabelWidget>("CharacterSkinItem.Text")!.Text =
                        CharacterSkinsManager.GetDisplayName((string)item);
                    obj.Children.Find<LabelWidget>("CharacterSkinItem.Details")!.Text =
                        $"{texture.Width}x{texture.Height}";
                    var playerModelWidget = obj.Children.Find<PlayerModelWidget>("CharacterSkinItem.Model")!;
                    playerModelWidget.PlayerClass = _playerData.PlayerClass;
                    playerModelWidget.CharacterSkinTexture = texture;
                    return obj;
                },
                delegate(object item)
                {
                    _playerData.CharacterSkinName = (string)item;
                    if (_playerData.IsDefaultName)
                    {
                        _playerData.ResetName();
                    }
                }
            );
            DialogsManager.ShowDialog(null, dialog);
        }

        if (_controlsButton.IsClicked)
        {
            DialogsManager.ShowDialog(
                null,
                new ListSelectionDialog(LanguageControl.Get(_typeName, 2), _inputDevices,
                    56f,
                    d => GetDeviceDisplayName((WidgetInputDevice)d),
                    delegate(object d)
                    {
                        var widgetInputDevice = (WidgetInputDevice)d;
                        _playerData.InputDevice = widgetInputDevice;
                        foreach (var playersDatum in _playerData.SubsystemPlayers.PlayersData)
                        {
                            if (playersDatum != _playerData && (playersDatum.InputDevice & widgetInputDevice) != 0)
                            {
                                playersDatum.InputDevice &= ~widgetInputDevice;
                            }
                        }
                    }
                )
            );
        }

        if (_addButton.IsClicked && VerifyName())
        {
            if (CommonLib.WorkType > 0)
            {
                DialogsManager.Alert("不可进行操作");
            }
            else
            {
                _playerData.SubsystemPlayers.AddPlayerData(_playerData);
                ScreensManager.SwitchScreen("Players", _playerData.SubsystemPlayers);
            }
        }

        if (_deleteButton.IsClicked)
        {
            if (CommonLib.WorkType == WorkType.Server)
            {
                DialogsManager.Confirm("你确定要踢出这个玩家吗", btn =>
                {
                    var client = CommonLib.Net.GetClientByGUID(_playerData.PlayerGUID);
                    if (btn == MessageDialogButton.Button1)
                    {
                        if (client is not null)
                        {
                            CommonLib.Net.RemoveClient(client);
                        }
                    }
                    else
                    {
                        DialogsManager.HideAllDialogs();
                    }
                });
            }
            else if (CommonLib.WorkType == WorkType.Client)
            {
                DialogsManager.Alert("不可进行操作");
            }
            else
            {
                DialogsManager.Confirm(LanguageControl.Get(_typeName, 3), btn =>
                {
                    if (btn != MessageDialogButton.Button1)
                    {
                        return;
                    }

                    _playerData.SubsystemPlayers.RemovePlayerData(_playerData);
                    ScreensManager.SwitchScreen("Players", _playerData.SubsystemPlayers);
                });
            }
        }

        if (_playButton.IsClicked && VerifyName())
        {
            if (_mode == Mode.Initial)
            {
                if (CommonLib.WorkType == WorkType.Client)
                {
                    CommonLib.Net.QueuePackage(new PlayerDataPackage(_playerData, PlayerDataPackage.DataType.Create));
                    DialogsManager.ShowDialog(this, _fetchingPlayerDialog);
                }
                else
                {
                    _playerData.SetMain();
                    _playerData.SubsystemPlayers.AddPlayerData(_playerData);
                    ScreensManager.SwitchScreen("Game");
                }
            }
        }

        if (_addAnotherButton.IsClicked && VerifyName())
        {
            _playerData.SubsystemPlayers.AddPlayerData(_playerData);
            ScreensManager.SwitchScreen("Player", Mode.Initial, _playerData.SubsystemPlayers.Project);
        }

        if ((Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked) && VerifyName())
        {
            if (_mode == Mode.Initial)
            {
                GameManager.SaveProject(true, true);
                GameManager.DisposeProject();
                CommonLib.Net.Stop();
                ScreensManager.SwitchScreen("MainMenu");
            }
            else if (_mode is Mode.Add or Mode.Edit)
            {
                ScreensManager.SwitchScreen("Players", _playerData.SubsystemPlayers);
            }

            if (_mode == Mode.Edit)
            {
                CommonLib.Net.QueuePackage(new PlayerDataPackage(_playerData, PlayerDataPackage.DataType.Modify));
            }
        }

        _nameWasInvalid = false;
    }

    public static string GetDeviceDisplayName(WidgetInputDevice device)
    {
        return device switch
        {
            WidgetInputDevice.Keyboard | WidgetInputDevice.Mouse => LanguageControl.Get(_typeName, 4),
            WidgetInputDevice.GamePad1 => LanguageControl.Get(_typeName, 5) +
                                          (GamePad.IsConnected(0) ? "" : LanguageControl.Get(_typeName, 9)),
            WidgetInputDevice.GamePad2 => LanguageControl.Get(_typeName, 6) +
                                          (GamePad.IsConnected(1) ? "" : LanguageControl.Get(_typeName, 9)),
            WidgetInputDevice.GamePad3 => LanguageControl.Get(_typeName, 7) +
                                          (GamePad.IsConnected(2) ? "" : LanguageControl.Get(_typeName, 9)),
            WidgetInputDevice.GamePad4 => LanguageControl.Get(_typeName, 8) +
                                          (GamePad.IsConnected(3) ? "" : LanguageControl.Get(_typeName, 9)),
            WidgetInputDevice.VrControllers => LanguageControl.Get(_typeName, 11) +
                                               (VrManager.IsVrAvailable ? "" : LanguageControl.Get(_typeName, 9)),
            _ => LanguageControl.Get(_typeName, 10)
        };
    }

    public bool VerifyName()
    {
        if (_nameWasInvalid)
        {
            return false;
        }

        if (PlayerData.VerifyName(_nameTextBox.Text.Trim()))
        {
            return true;
        }

        DialogsManager.Alert(LanguageControl.Get(_typeName, 12));
        return false;
    }
}
