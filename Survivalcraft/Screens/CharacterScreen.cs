using System.Xml.Linq;

using Engine.Input;

using EntitySystem.Core;

using Game.Commands;
using Game.Network;

namespace Game.Screens;

public class CharacterScreen : Screen
{
    public enum Mode
    {
        Initial = 0,

        Edit = 1
    }

    private const string _typeName = nameof(CharacterScreen);

    private readonly BusyDialog _fetchingPlayerDialog = new(
        LanguageManager.GetContentWidgets(nameof(CharacterScreen), "CreatingCharacter"),
        string.Empty
    );

    private double _creationStartedAt;

    private bool _creatingCharacter;

    private readonly ClickableWidget _characterSkinSelector;

    private readonly LabelWidget _characterSkinLabel;

    private readonly CharacterSkinsCache _characterSkinsCache;

    private readonly SelectionDrawerWidget _controlsDrawer;

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

    private readonly ButtonWidget _characterSexButton;

    private readonly ClickableTextRowWidget _characterSexLabel;

    private CharacterData _characterData = null!;

    private readonly CharacterModelWidget _playerModel;

    public CharacterScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/CharacterScreen");
        LoadContents(this, node);
        _playerModel = Children.Find<CharacterModelWidget>("Model")!;
        _characterSexButton = Children.Find<ButtonWidget>("CharacterSexButton")!;
        _characterSexLabel = Children.Find<ClickableTextRowWidget>("CharacterSexLabel")!;
        _nameTextBox = Children.Find<TextBoxWidget>("Name")!;
        _characterSkinLabel = Children.Find<LabelWidget>("CharacterSkinLabel")!;
        _characterSkinSelector = Children.Find<ClickableWidget>("CharacterSkinSelector")!;
        _controlsDrawer = Children.Find<SelectionDrawerWidget>("Controls")!;
        _controlsDrawer.ItemTextProvider = item => GetDeviceDisplayName((WidgetInputDevice)item);
        _controlsDrawer.SetItems(_inputDevices.Cast<object>());
        _controlsDrawer.SelectionChanged += UpdateInputDevice;
        _descriptionLabel = Children.Find<LabelWidget>("DescriptionLabel")!;
        _playButton = Children.Find<ButtonWidget>("PlayButton")!;
        _characterSkinsCache = new CharacterSkinsCache();
        _playerModel.CharacterSkinsCache = _characterSkinsCache;
        _nameTextBox.FocusLost += delegate
        {
            if (VerifyName())
            {
                _characterData.Name = _nameTextBox.Text.Trim();
            }
            else
            {
                _nameWasInvalid = true;
            }
        };
    }

    public override void Enter(object[] parameters)
    {
        _mode = (Mode)parameters[0];
        _characterData = _mode == Mode.Edit ? (CharacterData)parameters[1] : new CharacterData((Project)parameters[1]);
        if (_mode == Mode.Edit &&
            (!ReferenceEquals(_characterData.Client, CommonLib.Net.Self) || CommonLib.Net.Self?.PlayerAddress is null))
        {
            throw new InvalidOperationException("Only the current session's character can be edited here.");
        }

        _characterData.SubsystemCharacters.CharacterCreationRejected += OnCharacterCreationRejected;
        _creatingCharacter = _characterData.SubsystemCharacters.PendingCharacterCreationId != Guid.Empty;
        _creationStartedAt = Time.RealTime;
        if (_creatingCharacter) { DialogsManager.ShowDialog(this, _fetchingPlayerDialog); }

        _descriptionLabel.Text = string.Empty;
        _controlsDrawer.RefreshItems();

        if (_mode == Mode.Initial)
        {
            _characterSexButton.IsEnabled = true;
            _playButton.IsVisible = true;
        }
        else if (_mode == Mode.Edit)
        {
            _characterSexButton.IsEnabled = false;
            _playButton.IsVisible = false;
        }
    }

    public override void Leave()
    {
        _characterData.SubsystemCharacters.CharacterCreationRejected -= OnCharacterCreationRejected;
        _characterData.SubsystemCharacters.ClearCharacterCreationRequest();
        DialogsManager.HideDialog(_fetchingPlayerDialog);
        _creatingCharacter = false;
        _characterSkinsCache.Clear();
        _characterData = null!;
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

        if (_mode == Mode.Initial &&
            _characterData.SubsystemCharacters.FindCharacterData(player => player.IsLocalCharacter) is not null)
        {
            DialogsManager.HideDialog(_fetchingPlayerDialog);
            ScreensManager.SwitchScreen("Game", CommonLib.WorkType);
            return;
        }

        if (_creatingCharacter)
        {
            if (Time.RealTime - _creationStartedAt >= 15)
            {
                _characterData.SubsystemCharacters.ClearCharacterCreationRequest();
                _creatingCharacter = false;
                DialogsManager.HideDialog(_fetchingPlayerDialog);
                DialogsManager.Alert(LanguageManager.GetContentWidgets(_typeName, "CreationTimeout"));
            }

            return;
        }

        _characterSkinsCache.GetTexture(_characterData.CharacterSkinName);
        _playerModel.CharacterSex = _characterData.CharacterSex;
        _playerModel.CharacterSkinName = _characterData.CharacterSkinName;
        _characterSexButton.Text = _characterData.CharacterSex.ToString();
        if (!_nameTextBox.HasFocus)
        {
            _nameTextBox.Text = _characterData.Name;
        }

        _characterSkinLabel.Text = CharacterSkinsManager.GetDisplayName(_characterData.CharacterSkinName);
        var inputDevice = _inputDevices.FirstOrDefault(id => (id & _characterData.InputDevice) != 0);
        if (!Equals(_controlsDrawer.SelectedItem, inputDevice))
        {
            _controlsDrawer.SelectedItem = inputDevice;
        }

        if (_characterSexLabel.IsClicked)
        {
            UpdateCharacterSexDescription();
        }

        if (_characterSexButton.IsClicked)
        {
            _characterData.CharacterSex =
                _characterData.CharacterSex == CharacterSex.Male ? CharacterSex.Female : CharacterSex.Male;
            _characterData.RandomizeCharacterSkin();
            if (_characterData.IsDefaultName)
            {
                _characterData.ResetName();
            }
        }

        if (_characterSkinSelector.IsClicked)
        {
            CharacterSkinsManager.UpdateCharacterSkinsList();
            var items = CharacterSkinsManager.ReadOnlyCharacterSkinsNames.Where(n =>
                CharacterSkinsManager.GetCharacterSex(n) == _characterData.CharacterSex ||
                !CharacterSkinsManager.GetCharacterSex(n).HasValue
            );
            var dialog = new ListSelectionDialog(
                LanguageManager.Get(_typeName, 1),
                items,
                64f,
                delegate(object item)
                {
                    var node = ContentManager.Get<XElement>("Widgets/CharacterSkinItem");
                    var obj = (ContainerWidget)LoadWidget(this, node, null);
                    var texture = _characterSkinsCache.GetTexture((string)item);
                    obj.Children.Find<LabelWidget>("CharacterSkinItem.Text")!.Text =
                        CharacterSkinsManager.GetDisplayName((string)item);
                    obj.Children.Find<LabelWidget>("CharacterSkinItem.Details")!.Text =
                        $"{texture.Width}x{texture.Height}";
                    var playerModelWidget = obj.Children.Find<CharacterModelWidget>("CharacterSkinItem.Model")!;
                    playerModelWidget.CharacterSex = _characterData.CharacterSex;
                    playerModelWidget.CharacterSkinTexture = texture;
                    return obj;
                },
                delegate(object item)
                {
                    _characterData.CharacterSkinName = (string)item;
                    if (_characterData.IsDefaultName)
                    {
                        _characterData.ResetName();
                    }
                }
            );
            DialogsManager.ShowDialog(null, dialog);
        }

        if (_playButton.IsClicked && VerifyName())
        {
            if (_mode == Mode.Initial)
            {
                if (CommonLib.WorkType == WorkType.Client)
                {
                    _characterData.SubsystemCharacters.RequestOwnCharacterCreation(_characterData);
                    _creatingCharacter = true;
                    _creationStartedAt = Time.RealTime;
                    DialogsManager.ShowDialog(this, _fetchingPlayerDialog);
                }
                else
                {
                    _characterData.SetMain();
                    _characterData.SubsystemCharacters.AddCharacterData(_characterData);
                    ScreensManager.SwitchScreen("Game");
                }
            }
        }

        if ((Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked) &&
            (_mode == Mode.Initial || VerifyName()))
        {
            if (_mode == Mode.Initial)
            {
                GameManager.SaveProject(true, true);
                GameManager.DisposeProject();
                CommonLib.Net.Stop();
                ScreensManager.SwitchScreen("Play");
            }
            else
            {
                CommandGateway.Submit(
                    _characterData,
                    new UpdateOwnPlayerProfileCommand(
                        _characterData.Name,
                        _characterData.CharacterSkinName,
                        _characterData.CharacterSex
                    )
                );
                ScreensManager.SwitchScreen("Game");
            }
        }

        _nameWasInvalid = false;
    }

    private void UpdateInputDevice()
    {
        if (_controlsDrawer.SelectedItem is not WidgetInputDevice inputDevice || _characterData is null)
        {
            return;
        }

        _characterData.InputDevice = inputDevice;
    }

    private void UpdateCharacterSexDescription()
    {
        var valuesDictionary = DatabaseManager.FindValuesDictionaryForComponent(
            DatabaseManager.FindEntityValuesDictionary(_characterData.GetEntityTemplateName(), true)!,
            typeof(ComponentCreature)
        );
        if (valuesDictionary == null)
        {
            return;
        }

        var description = valuesDictionary.GetValue<string>("Description");
        if (description.StartsWith('[') && description.EndsWith(']'))
        {
            var parts = description[1..^1].Split([":"], StringSplitOptions.RemoveEmptyEntries);
            description = LanguageManager.GetDatabase("Description", parts[1]);
        }

        _descriptionLabel.Text = description;
    }

    public static string GetDeviceDisplayName(WidgetInputDevice device)
    {
        return device switch
        {
            WidgetInputDevice.Keyboard | WidgetInputDevice.Mouse => LanguageManager.Get(_typeName, 4),
            WidgetInputDevice.GamePad1 => LanguageManager.Get(_typeName, 5) +
                                          (GamePad.IsConnected(0) ? "" : LanguageManager.Get(_typeName, 9)),
            WidgetInputDevice.GamePad2 => LanguageManager.Get(_typeName, 6) +
                                          (GamePad.IsConnected(1) ? "" : LanguageManager.Get(_typeName, 9)),
            WidgetInputDevice.GamePad3 => LanguageManager.Get(_typeName, 7) +
                                          (GamePad.IsConnected(2) ? "" : LanguageManager.Get(_typeName, 9)),
            WidgetInputDevice.GamePad4 => LanguageManager.Get(_typeName, 8) +
                                          (GamePad.IsConnected(3) ? "" : LanguageManager.Get(_typeName, 9)),
            WidgetInputDevice.VrControllers => LanguageManager.Get(_typeName, 11) +
                                               (VrManager.IsVrAvailable ? "" : LanguageManager.Get(_typeName, 9)),
            _ => LanguageManager.Get(_typeName, 10)
        };
    }

    private void OnCharacterCreationRejected(CharacterCreationError error)
    {
        _creatingCharacter = false;
        DialogsManager.HideDialog(_fetchingPlayerDialog);
        var key = error switch
        {
            CharacterCreationError.ExistingCharacter => "CreationExistingCharacter",
            CharacterCreationError.InvalidProfile => "CreationInvalidProfile",
            CharacterCreationError.NameUnavailable => "CreationNameUnavailable",
            _ => "CreationUnavailable"
        };
        DialogsManager.Alert(LanguageManager.GetContentWidgets(_typeName, key));
    }

    public bool VerifyName()
    {
        if (_nameWasInvalid)
        {
            return false;
        }

        string name;
        try { name = CharacterData.SanitizeName(_nameTextBox.Text.Trim()); }
        catch (InvalidOperationException)
        {
            DialogsManager.Alert(LanguageManager.GetContentWidgets(_typeName, "CreationInvalidProfile"));
            return false;
        }

        if (CharacterData.VerifyName(name))
        {
            if (_characterData.SubsystemCharacters.IsCharacterNameAvailable(name, _characterData.CharacterId))
            {
                _nameTextBox.Text = name;
                _characterData.Name = name;
                return true;
            }

            DialogsManager.Alert(LanguageManager.GetContentWidgets(_typeName, "CreationNameUnavailable"));
            return false;
        }

        DialogsManager.Alert(LanguageManager.Get(_typeName, 12));
        return false;
    }
}
