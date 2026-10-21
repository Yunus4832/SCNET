using System.Globalization;
using System.Xml.Linq;

using Engine.Network;

using Game.Servers;

using Rendezvous.Protocol;

namespace Game.Screens;

public class WorldServerSettingsScreen : Screen
{
    private const string _typeName = nameof(WorldServerSettingsScreen);

    private readonly SliderWidget _daySpeedSlider;

    private readonly TextBoxWidget _disableBlocks;

    private readonly TextBoxWidget _keywordBlocking;

    private readonly LabelWidget _descriptionLabel;

    private readonly (ClickableTextRowWidget Label, string DescriptionName)[] _descriptionBindings;

    private readonly SliderWidget _maxPlayersSlider;

    private readonly ButtonWidget _randomSpawnPositionButton;

    private readonly SliderWidget _recoverySpeedSlider;

    private readonly ButtonWidget _runServerButton;

    private readonly ContainerWidget _serverOptions;

    private readonly ButtonWidget _p2pButton;

    private readonly ContainerWidget _p2pOptions;

    private readonly InlineSelectionWidget _p2pService;

    private bool _refreshingP2PServices;

    private readonly ButtonWidget _p2pPublic;

    private readonly TextBoxWidget _p2pName;

    private readonly TextBoxWidget _p2pDescription;

    private string _returnScreenName = "NewWorld";

    private object[] _returnParameters = [];

    private WorldSettings _worldSettings = null!;

    public WorldServerSettingsScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/WorldServerSettingsScreen");
        LoadContents(this, node);
        _runServerButton = Children.Find<ButtonWidget>("RunServer")!;
        _serverOptions = Children.Find<ContainerWidget>("ServerOptions")!;
        _p2pButton = Children.Find<ButtonWidget>("P2P")!;
        _p2pOptions = Children.Find<ContainerWidget>("P2POptions")!;
        _p2pService = Children.Find<InlineSelectionWidget>("P2PService")!;
        _p2pService.ItemTextProvider = item => item is InstalledRendezvousService service
            ? service.Name
            : LanguageManager.GetContentWidgets(nameof(ContentScreen), "ManageRendezvous");
        _p2pService.SelectionChanged += SelectP2PService;
        _p2pPublic = Children.Find<ButtonWidget>("P2PPublic")!;
        _p2pName = Children.Find<TextBoxWidget>("P2PName")!;
        _p2pDescription = Children.Find<TextBoxWidget>("P2PDescription")!;
        _p2pName.MaximumLength = RendezvousLimits.MaximumRoomNameLength;
        _p2pDescription.MaximumLength = RendezvousLimits.MaximumRoomDescriptionLength;
        _randomSpawnPositionButton = Children.Find<ButtonWidget>("RandomSpawnPosition")!;
        _maxPlayersSlider = Children.Find<SliderWidget>("MaxPlayers")!;
        _maxPlayersSlider.MaxValue = WorldSettings.MaximumOnlinePlayerCount;
        _daySpeedSlider = Children.Find<SliderWidget>("DaySpeed")!;
        _recoverySpeedSlider = Children.Find<SliderWidget>("RecoverySpeed")!;
        _disableBlocks = Children.Find<TextBoxWidget>("DisableBlocks")!;
        _keywordBlocking = Children.Find<TextBoxWidget>("KeywordBlocking")!;
        _descriptionLabel = Children.Find<LabelWidget>("Description")!;
        _descriptionBindings =
        [
            CreateDescriptionBinding("RunServerLabel", "RunServer"),
            CreateDescriptionBinding("MaxPlayersLabel", "MaxPlayers"),
            CreateDescriptionBinding("DaySpeedLabel", "DaySpeed"),
            CreateDescriptionBinding("RecoverySpeedLabel", "RecoverySpeed"),
            CreateDescriptionBinding("DisableBlocksLabel", "DisableBlocks"),
            CreateDescriptionBinding("RandomSpawnPositionLabel", "RandomSpawnPosition"),
            CreateDescriptionBinding("KeywordBlockingLabel", "KeywordBlocking")
        ];

        _disableBlocks.MaximumLength = int.MaxValue;
    }

    public override void Enter(object[] parameters)
    {
        _worldSettings = parameters.Length > 0 && parameters[0] is WorldSettings worldSettings
            ? worldSettings
            : new WorldSettings { RunServer = true };
        _returnScreenName = parameters.Length > 1 && parameters[1] is string returnScreenName
            ? returnScreenName
            : "NewWorld";
        _returnParameters = parameters.Length > 2 ? new object[parameters.Length - 2] : [_worldSettings];
        if (parameters.Length > 2)
        {
            Array.Copy(parameters, 2, _returnParameters, 0, _returnParameters.Length);
        }

        _maxPlayersSlider.Value = _worldSettings.MaxOnlinePlayerCount;
        _daySpeedSlider.Value = NormalizeDaySpeed(_worldSettings.DaySpeed);
        _recoverySpeedSlider.Value = NormalizeRecoverySpeed(_worldSettings.RecoverFactor);
        _disableBlocks.Text = _worldSettings.DisableBlocks;
        _keywordBlocking.Text = _worldSettings.KeywordBlocking;
        _p2pName.Text = string.IsNullOrEmpty(_worldSettings.P2P.Name) ? _worldSettings.Name : _worldSettings.P2P.Name;
        _p2pDescription.Text = _worldSettings.P2P.Description;
        _descriptionLabel.Text = GetText("DefaultDescription");
        RefreshP2PServices();
        UpdateControlTexts();
    }

    public override void Update()
    {
        if (_p2pButton.IsClicked)
        {
            _worldSettings.P2P = _worldSettings.P2P with { Enabled = !_worldSettings.P2P.Enabled };
        }

        if (_p2pPublic.IsClicked)
        {
            _worldSettings.P2P = _worldSettings.P2P with { IsPublic = !_worldSettings.P2P.IsPublic };
        }

        if (_runServerButton.IsClicked)
        {
            _worldSettings.RunServer = !_worldSettings.RunServer;
        }

        if (_randomSpawnPositionButton.IsClicked)
        {
            _worldSettings.RandomSpawnPosition = !_worldSettings.RandomSpawnPosition;
        }

        SaveSettings();
        UpdateControlTexts();
        UpdateSelectedDescription();

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            if (ValidateP2PSettings())
            {
                ScreensManager.SwitchScreen(_returnScreenName, _returnParameters);
            }
        }
    }

    private void RefreshP2PServices()
    {
        _refreshingP2PServices = true;
        try
        {
            _p2pService.Close();
            _p2pService.SelectedItem = null;
            var services = RendezvousManager.Current.Snapshot().Where(item => item.IsEnabled).ToArray();
            _p2pService.SetItems(services.Cast<object>().Append("manage"));
            _p2pService.SelectedItem = services.FirstOrDefault(item =>
                item.ControlUrl == _worldSettings.P2P.ControlUrl && item.Name == _worldSettings.P2P.ServiceName
            );
        }
        finally
        {
            _refreshingP2PServices = false;
        }
    }

    private void SelectP2PService()
    {
        if (_refreshingP2PServices)
        {
            return;
        }

        if (_p2pService.SelectedItem is InstalledRendezvousService selected)
        {
            _worldSettings.P2P = _worldSettings.P2P.SelectService(selected);
        }
        else if (_p2pService.SelectedItem is string)
        {
            SaveSettings();
            var arguments = new object[] { _worldSettings, _returnScreenName }.Concat(_returnParameters).ToArray();
            ScreensManager.SwitchScreen("ManageRendezvous", "WorldServerSettings", arguments);
        }
    }

    private bool ValidateP2PSettings()
    {
        if (!_worldSettings.RunServer || !_worldSettings.P2P.Enabled)
        {
            return true;
        }

        try
        {
            var address = RendezvousAddress.Normalize(new Uri(_worldSettings.P2P.ControlUrl));
            if (string.IsNullOrWhiteSpace(_worldSettings.P2P.Name))
            {
                throw new ArgumentException("Room name is required.");
            }

            _worldSettings.P2P = _worldSettings.P2P with { ControlUrl = address.AbsoluteUri };
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            DialogsManager.Alert(GetText("P2PInvalidSettings"));
            return false;
        }
    }

    private void SaveSettings()
    {
        _worldSettings.P2P = _worldSettings.P2P with
        {
            Name = _p2pName.Text.Trim(), Description = _p2pDescription.Text
        };
        _worldSettings.MaxOnlinePlayerCount = (ushort)MathUtils.Round(_maxPlayersSlider.Value);
        _worldSettings.DisableBlocks = _disableBlocks.Text;
        _worldSettings.KeywordBlocking = _keywordBlocking.Text;

        _worldSettings.DaySpeed = _daySpeedSlider.Value;
        _worldSettings.RecoverFactor = _recoverySpeedSlider.Value;
    }

    private void UpdateControlTexts()
    {
        _serverOptions.IsEnabled = _worldSettings.RunServer;
        _p2pOptions.IsEnabled = _worldSettings.P2P.Enabled;
        if (!_worldSettings.RunServer || !_worldSettings.P2P.Enabled)
        {
            _p2pService.Close();
        }

        _p2pPublic.Text = LanguageManager.Get("Usual", _worldSettings.P2P.IsPublic ? "yes" : "no");
        _p2pService.PlaceholderText = string.IsNullOrEmpty(_worldSettings.P2P.ControlUrl)
            ? GetText("P2PSelectService")
            : string.IsNullOrEmpty(_worldSettings.P2P.ServiceName)
                ? _worldSettings.P2P.ControlUrl
                : _worldSettings.P2P.ServiceName;
        _p2pButton.Text = _worldSettings.P2P.Enabled
            ? LanguageManager.Get("Usual", "on")
            : LanguageManager.Get("Usual", "off");
        _runServerButton.Text = _worldSettings.RunServer
            ? LanguageManager.Get("Usual", "on")
            : LanguageManager.Get("Usual", "off");
        _randomSpawnPositionButton.Text = _worldSettings.RandomSpawnPosition
            ? LanguageManager.Get("Usual", "on")
            : LanguageManager.Get("Usual", "off");
        _maxPlayersSlider.Text = MathUtils.Round(_maxPlayersSlider.Value).ToString(CultureInfo.InvariantCulture);
        _daySpeedSlider.Text = $"{_daySpeedSlider.Value:0}x";
        _recoverySpeedSlider.Text = $"{_recoverySpeedSlider.Value:0}x";
    }

    private (ClickableTextRowWidget Label, string DescriptionName) CreateDescriptionBinding(
        string labelName,
        string descriptionName
    )
    {
        return (Children.Find<ClickableTextRowWidget>(labelName)!, descriptionName);
    }

    private void UpdateSelectedDescription()
    {
        foreach (var (label, descriptionName) in _descriptionBindings)
        {
            if (label.IsClicked)
            {
                SetDescription(descriptionName);
                break;
            }
        }
    }

    private void SetDescription(string name)
    {
        _descriptionLabel.Text = GetText($"{name}Description");
    }

    private static string GetText(string name)
    {
        return LanguageManager.GetContentWidgets(_typeName, name);
    }

    private static float NormalizeDaySpeed(float daySpeed)
    {
        return daySpeed < 1f || daySpeed > 10f ? 1f : daySpeed;
    }

    private static float NormalizeRecoverySpeed(float recoverySpeed)
    {
        return recoverySpeed < 1f || recoverySpeed > 10f ? 1f : recoverySpeed;
    }
}
