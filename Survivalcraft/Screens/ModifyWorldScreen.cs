using System.Xml.Linq;

using EntitySystem.TemplatesDatabase;

using Game.Admission;
using Game.Application;

namespace Game.Screens;

public class ModifyWorldScreen : Screen
{
    private WorldAdmissionDraft _admission = new();

    private string _originalAdmission = string.Empty;

    private const string _typeName = nameof(ModifyWorldScreen);

    // 新增一个布尔变量，用于控制是否允许在残酷模式下更改游戏模式
    private const bool _allowGameModeChangeInCruelMode = false;

    // 其他模式能否改残酷模式
    private const bool _cruelAllowed = true;

    private readonly ButtonWidget _applyButton;

    private bool _changingGameModeAllowed;

    private readonly ValuesDictionary _currentWorldSettingsData = new();

    private WorldHostingSettings _hosting = new();

    private string _directoryName = string.Empty;

    private readonly LabelWidget _errorLabel;

    private readonly ButtonWidget _gameModeButton;

    private readonly ButtonWidget _modsButton;

    private ModProfile _modProfile = new();

    private ModProfile _originalProfile = new();

    private bool _busy;

    private readonly TextBoxWidget _nameTextBox;

    private readonly ValuesDictionary _originalWorldSettingsData = new();

    private readonly LabelWidget _seedLabel;

    private readonly ButtonWidget _serverSettingsButton;

    private readonly ButtonWidget _worldOptionsButton;

    private WorldSettings WorldSettings
    {
        get => field is not null ? field : throw new InvalidOperationException("WorldSetting is not initialized");
        set;
    }

    public ModifyWorldScreen()
    {
        var node = AssetManager.Get<XElement>("Screens/ModifyWorldScreen");
        LoadContents(this, node);
        _nameTextBox = Children.Find<TextBoxWidget>("Name")!;
        _seedLabel = Children.Find<LabelWidget>("Seed")!;
        _gameModeButton = Children.Find<ButtonWidget>("GameMode")!;
        _modsButton = Children.Find<ButtonWidget>("Mods")!;
        _worldOptionsButton = Children.Find<ButtonWidget>("WorldOptions")!;
        _serverSettingsButton = Children.Find<ButtonWidget>("ServerSettings")!;
        _errorLabel = Children.Find<LabelWidget>("Error")!;
        _applyButton = Children.Find<ButtonWidget>("Apply")!;
        _nameTextBox.TextChanged += delegate { WorldSettings.Name = _nameTextBox.Text; };
    }

    public override void Enter(object[] parameters)
    {
        if (ScreensManager.PreviousScreen == null ||
            ScreensManager.PreviousScreen is WorldOptionsScreen or WorldModsScreen)
        {
            return;
        }

        var isReturningFromServerSettings =
            ScreensManager.PreviousScreen?.GetType() == typeof(WorldServerSettingsScreen);
        _directoryName = (string)parameters[0];
        WorldSettings = (WorldSettings)parameters[1];
        _hosting = parameters.OfType<WorldHostingSettings>().FirstOrDefault() ?? _hosting;
        if (!isReturningFromServerSettings)
        {
            _originalProfile = ModProfileManager.LoadWorldProfile(_directoryName) ??
                               throw new InvalidOperationException("World mod profile is missing.");
            _modProfile = ModProfileManager.CreateSessionProfile("draft", _originalProfile);
            _admission = new WorldAdmissionDraft
                { Configuration = ClientApplication.Worlds.ReadAdmissionPolicy(_directoryName) };
            _originalAdmission = _admission.Configuration.Fingerprint();
            _originalWorldSettingsData.Clear();
            WorldSettings.Save(_originalWorldSettingsData, true);
            _hosting.Save(_originalWorldSettingsData, WorldSettings.Name);
        }

        // 修改逻辑：根据 allowGameModeChangeInCruelMode 的值来决定是否允许更改游戏模式
        _changingGameModeAllowed = _allowGameModeChangeInCruelMode || WorldSettings.GameMode != GameMode.Cruel;
    }

    public override void Update()
    {
        if (_busy)
        {
            return;
        }

        if (_gameModeButton.IsClicked && _changingGameModeAllowed)
        {
            DialogsManager.ShowDialog(
                null,
                new SelectGameModeDialog(
                    Localization.ClientLanguage.Catalog.GetContentWidgets(_typeName, 3),
                    _cruelAllowed,
                    delegate(GameMode gameMode) { WorldSettings.GameMode = gameMode; },
                    WorldSettings.GameMode
                )
            );
        }

        _currentWorldSettingsData.Clear();
        WorldSettings.Save(_currentWorldSettingsData, true);
        _hosting.Save(_currentWorldSettingsData, WorldSettings.Name);
        var hasChanges = !CompareValueDictionaries(_originalWorldSettingsData, _currentWorldSettingsData) ||
                         _originalAdmission != _admission.Configuration.Fingerprint() ||
                         !ModProfileIdentity.AreEquivalent(_originalProfile, _modProfile);
        var isNameValid = ClientApplication.Worlds.ValidateWorldName(WorldSettings.Name);
        _nameTextBox.Text = WorldSettings.Name;
        _seedLabel.Text = WorldSettings.Seed;
        _gameModeButton.Text =
            Localization.ClientLanguage.Catalog.Get("GameMode", WorldSettings.GameMode.ToString());
        _gameModeButton.IsEnabled = _changingGameModeAllowed;
        _errorLabel.IsVisible = !isNameValid;
        _applyButton.IsEnabled = isNameValid && hasChanges;
        if (_worldOptionsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("WorldOptions", WorldSettings, true);
        }

        if (_serverSettingsButton.IsClicked)
        {
            ScreensManager.SwitchScreen(
                "WorldServerSettings",
                WorldSettings,
                "ModifyWorld",
                _directoryName,
                WorldSettings,
                _admission,
                _hosting
            );
        }

        if (_modsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("WorldMods", _modProfile, "ModifyWorld", _originalProfile);
        }

        if (_applyButton.IsClicked && isNameValid && hasChanges)
        {
            if (ModProfileIdentity.AreEquivalent(_originalProfile, _modProfile))
            {
                ApplyEdits(null);
            }
            else
            {
                var originalName = _originalWorldSettingsData.GetValue<string>("WorldName");
                var cloneName = WorldRepository.GetCloneName(originalName, DateTime.Now);
                var language = Localization.ClientLanguage.Catalog;
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        language.GetContentWidgets("WorldModsScreen", "ConfirmApply"),
                        string.Format(language.GetContentWidgets("WorldModsScreen", "CloneBeforeApply"), cloneName),
                        language.Ok,
                        language.Cancel,
                        button =>
                        {
                            if (button == MessageDialogButton.Button1) { ApplyEdits(cloneName); }
                        }
                    )
                );
            }
        }

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            if (hasChanges)
            {
                DialogsManager.ShowDialog(
                    null,
                    new MessageDialog(
                        Localization.ClientLanguage.Catalog.Get(_typeName, 3),
                        Localization.ClientLanguage.Catalog.Get(_typeName, 4),
                        Localization.ClientLanguage.Catalog.Get("Usual", "yes"),
                        Localization.ClientLanguage.Catalog.Get("Usual", "no"),
                        delegate(MessageDialogButton button)
                        {
                            if (button == MessageDialogButton.Button1)
                            {
                                ScreensManager.SwitchScreen("Play");
                            }
                        }
                    )
                );
            }
            else
            {
                ScreensManager.SwitchScreen("Play");
            }
        }
    }

    private void ApplyEdits(string? cloneName)
    {
        if (WorldSettings.GameMode != GameMode.Creative && WorldSettings.GameMode != GameMode.Adventure)
        {
            WorldSettings.ResetOptionsForNonCreativeMode(creatingWorld: false);
        }

        _busy = true;
        var dialog = new BusyDialog(
            Localization.ClientLanguage.Catalog.GetContentWidgets("WorldModsScreen", "Applying"),
            string.Empty
        );
        DialogsManager.ShowDialog(null, dialog);
        Task.Run(() =>
            {
                try
                {
                    ClientApplication.Worlds.ApplyWorldEdit(
                        _directoryName,
                        WorldSettings,
                        _hosting,
                        _admission.Configuration,
                        _modProfile,
                        new LocalModRepository(Storage.GetSystemPath(ClientApplication.Paths.ContentPackageCache)),
                        cloneName
                    );
                    Dispatcher.Dispatch(() =>
                        {
                            _busy = false;
                            DialogsManager.HideDialog(dialog);
                            ScreensManager.SwitchScreen("Play");
                        }
                    );
                }
                catch (Exception exception)
                {
                    Dispatcher.Dispatch(() =>
                        {
                            _busy = false;
                            DialogsManager.HideDialog(dialog);
                            DialogsManager.Alert(
                                Localization.ClientLanguage.Catalog.GetContentWidgets("WorldModsScreen", "ApplyFailed"),
                                exception.Message
                            );
                        }
                    );
                }
            }
        );
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
