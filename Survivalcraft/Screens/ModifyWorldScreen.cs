using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

namespace Game.Screens;

public class ModifyWorldScreen : Screen
{
    private const string _typeName = "ModifyWorldScreen";

    // 新增一个布尔变量，用于控制是否允许在残酷模式下更改游戏模式
    private const bool _allowGameModeChangeInCruelMode = false;

    // 其他模式能否改残酷模式
    private const bool _cruelAllowed = true;

    private readonly ButtonWidget _applyButton;

    private bool _changingGameModeAllowed;

    private readonly ValuesDictionary _currentWorldSettingsData = new();

    private readonly ButtonWidget _deleteButton;

    private string _directoryName = string.Empty;

    private readonly LabelWidget _errorLabel;

    private readonly ButtonWidget _gameModeButton;

    private readonly TextBoxWidget _nameTextBox;

    private readonly ValuesDictionary _originalWorldSettingsData = new();

    private readonly LabelWidget _seedLabel;

    private readonly ButtonWidget _serverSettingsButton;

    private readonly ButtonWidget _uploadButton;

    private readonly ButtonWidget _worldOptionsButton;

    private WorldSettings WorldSettings
    {
        get => field is not null ? field : throw new InvalidOperationException("WorldSetting is not initialized");
        set;
    }

    public ModifyWorldScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/ModifyWorldScreen");
        LoadContents(this, node);
        _nameTextBox = Children.Find<TextBoxWidget>("Name")!;
        _seedLabel = Children.Find<LabelWidget>("Seed")!;
        _gameModeButton = Children.Find<ButtonWidget>("GameMode")!;
        _worldOptionsButton = Children.Find<ButtonWidget>("WorldOptions")!;
        _serverSettingsButton = Children.Find<ButtonWidget>("ServerSettings")!;
        _errorLabel = Children.Find<LabelWidget>("Error")!;
        _applyButton = Children.Find<ButtonWidget>("Apply")!;
        _deleteButton = Children.Find<ButtonWidget>("Delete")!;
        _uploadButton = Children.Find<ButtonWidget>("Upload")!;

        _nameTextBox.TextChanged += delegate { WorldSettings.Name = _nameTextBox.Text; };
    }

    public override void Enter(object[] parameters)
    {
        if (ScreensManager.PreviousScreen == null ||
            ScreensManager.PreviousScreen.GetType() == typeof(WorldOptionsScreen))
        {
            return;
        }

        var isReturningFromServerSettings =
            ScreensManager.PreviousScreen?.GetType() == typeof(WorldServerSettingsScreen);
        _directoryName = (string)parameters[0];
        WorldSettings = (WorldSettings)parameters[1];
        if (!isReturningFromServerSettings)
        {
            _originalWorldSettingsData.Clear();
            WorldSettings.Save(_originalWorldSettingsData, true);
        }

        // 修改逻辑：根据 allowGameModeChangeInCruelMode 的值来决定是否允许更改游戏模式
        _changingGameModeAllowed = _allowGameModeChangeInCruelMode || WorldSettings.GameMode != GameMode.Cruel;
    }

    public override void Update()
    {
        if (_gameModeButton.IsClicked && _changingGameModeAllowed)
        {
            DialogsManager.ShowDialog(null,
                new SelectGameModeDialog(string.Empty, _cruelAllowed,
                    delegate(GameMode gameMode) { WorldSettings.GameMode = gameMode; }));
        }

        _currentWorldSettingsData.Clear();
        WorldSettings.Save(_currentWorldSettingsData, true);
        var flag = !CompareValueDictionaries(_originalWorldSettingsData, _currentWorldSettingsData);
        var flag2 = WorldsManager.ValidateWorldName(WorldSettings.Name);
        _nameTextBox.Text = WorldSettings.Name;
        _seedLabel.Text = WorldSettings.Seed;
        _gameModeButton.Text = LanguageManager.Get("GameMode", WorldSettings.GameMode.ToString());
        _gameModeButton.IsEnabled = _changingGameModeAllowed;
        _errorLabel.IsVisible = !flag2;
        _uploadButton.IsEnabled = flag2 && !flag;
        _applyButton.IsEnabled = flag2 && flag;
        if (_worldOptionsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("WorldOptions", WorldSettings, true);
        }

        if (_serverSettingsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("WorldServerSettings", WorldSettings, "ModifyWorld", _directoryName,
                WorldSettings);
        }

        if (_deleteButton.IsClicked)
        {
            var dialog = new MessageDialog(
                LanguageManager.Get(_typeName, 1),
                LanguageManager.Get(_typeName, 2),
                LanguageManager.Get("Usual", "yes"),
                LanguageManager.Get("Usual", "no"),
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
                DialogsManager.ShowDialog(null, new MessageDialog(LanguageManager.Get(_typeName, 3),
                    LanguageManager.Get(_typeName, 4), LanguageManager.Get("Usual", "yes"),
                    LanguageManager.Get("Usual", "no"), delegate(MessageDialogButton button)
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
