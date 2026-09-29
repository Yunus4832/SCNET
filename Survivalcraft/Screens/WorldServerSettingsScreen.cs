using System.Globalization;
using System.Xml.Linq;

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

    private string _returnScreenName = "NewWorld";

    private object[] _returnParameters = [];

    private WorldSettings _worldSettings = null!;

    public WorldServerSettingsScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/WorldServerSettingsScreen");
        LoadContents(this, node);
        _runServerButton = Children.Find<ButtonWidget>("RunServer")!;
        _randomSpawnPositionButton = Children.Find<ButtonWidget>("RandomSpawnPosition")!;
        _maxPlayersSlider = Children.Find<SliderWidget>("MaxPlayers")!;
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

        _maxPlayersSlider.Value = MathUtils.Clamp(_worldSettings.MaxOnlinePlayerCount, 10, 100);
        _daySpeedSlider.Value = NormalizeDaySpeed(_worldSettings.DaySpeed);
        _recoverySpeedSlider.Value = NormalizeRecoverySpeed(_worldSettings.RecoverFactor);
        _disableBlocks.Text = _worldSettings.DisableBlocks;
        _keywordBlocking.Text = _worldSettings.KeywordBlocking;
        _descriptionLabel.Text = GetText("DefaultDescription");
    }

    public override void Update()
    {
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
            ScreensManager.SwitchScreen(_returnScreenName, _returnParameters);
        }
    }

    private void SaveSettings()
    {
        _worldSettings.MaxOnlinePlayerCount = (ushort)MathUtils.Round(_maxPlayersSlider.Value);
        _worldSettings.DisableBlocks = _disableBlocks.Text;
        _worldSettings.KeywordBlocking = _keywordBlocking.Text;

        _worldSettings.DaySpeed = _daySpeedSlider.Value;
        _worldSettings.RecoverFactor = _recoverySpeedSlider.Value;
    }

    private void UpdateControlTexts()
    {
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
        string descriptionName)
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
