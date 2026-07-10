using System.Globalization;
using System.Xml.Linq;

namespace Game.Screens;

public class WorldServerSettingsScreen : Screen
{
    private const string _typeName = nameof(WorldServerSettingsScreen);

    private readonly TextBoxWidget _daySpeedTextBox;

    private readonly TextBoxWidget _disableBlocks;

    private readonly TextBoxWidget _keywordBlocking;

    private readonly LabelWidget _descriptionLabel;

    private readonly TextBoxWidget _maxPlayer;

    private readonly CheckboxWidget _needLogin;

    private readonly TextBoxWidget _password;

    private readonly CheckboxWidget _randomSpawnPosition;

    private readonly TextBoxWidget _recoverySpeed;

    private readonly CheckboxWidget _runServer;

    private string _returnScreenName = "NewWorld";

    private object[] _returnParameters = [];

    private WorldSettings _worldSettings = null!;

    public WorldServerSettingsScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/WorldServerSettingsScreen");
        LoadContents(this, node);
        _runServer = Children.Find<CheckboxWidget>("RunServer")!;
        _needLogin = Children.Find<CheckboxWidget>("NeedLogin")!;
        _randomSpawnPosition = Children.Find<CheckboxWidget>("RandomSpawnPosition")!;
        _password = Children.Find<TextBoxWidget>("Password")!;
        _maxPlayer = Children.Find<TextBoxWidget>("MaxPlayers")!;
        _daySpeedTextBox = Children.Find<TextBoxWidget>("DaySpeed")!;
        _recoverySpeed = Children.Find<TextBoxWidget>("RecoverySpeed")!;
        _disableBlocks = Children.Find<TextBoxWidget>("DisableBlocks")!;
        _keywordBlocking = Children.Find<TextBoxWidget>("KeywordBlocking")!;
        _descriptionLabel = Children.Find<LabelWidget>("Description")!;

        _disableBlocks.MaximumLength = int.MaxValue;
        _password.Title = GetText("PasswordTitle");
        _maxPlayer.Title = GetText("MaxPlayersTitle");
        _daySpeedTextBox.Title = GetText("DaySpeed");
        _recoverySpeed.Title = GetText("RecoverySpeed");
        _disableBlocks.Title = GetText("DisableBlocksTitle");
        _keywordBlocking.Title = GetText("KeywordBlockingTitle");
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

        _runServer.IsChecked = _worldSettings.RunServer;
        _needLogin.IsChecked = _worldSettings.IsNeedCommunityLogin;
        _randomSpawnPosition.IsChecked = _worldSettings.RandomSpawnPosition;
        _password.Text = _worldSettings.Password;
        _maxPlayer.Text = MathUtils.Max(_worldSettings.MaxOnlinePlayerCount, 1).ToString(CultureInfo.InvariantCulture);
        _daySpeedTextBox.Text = NormalizeDaySpeed(_worldSettings.DaySpeed).ToString(CultureInfo.InvariantCulture);
        _recoverySpeed.Text = NormalizeRecoverySpeed(_worldSettings.RecoverFactor).ToString(CultureInfo.InvariantCulture);
        _disableBlocks.Text = _worldSettings.DisableBlocks;
        _keywordBlocking.Text = _worldSettings.KeywordBlocking;
        SetDescription("RunServer");
    }

    public override void Update()
    {
        SaveSettings();
        UpdateDescription();

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(_returnScreenName, _returnParameters);
        }
    }

    private void SaveSettings()
    {
        _worldSettings.RunServer = _runServer.IsChecked;
        int.TryParse(_maxPlayer.Text, out var maxPlayers);
        _worldSettings.MaxOnlinePlayerCount = (ushort)MathUtils.Max(maxPlayers, 1);
        _worldSettings.IsNeedCommunityLogin = _needLogin.IsChecked;
        _worldSettings.Password = _password.Text;
        _worldSettings.RandomSpawnPosition = _randomSpawnPosition.IsChecked;
        _worldSettings.DisableBlocks = _disableBlocks.Text;
        _worldSettings.KeywordBlocking = _keywordBlocking.Text;

        float.TryParse(_daySpeedTextBox.Text, out var daySpeed);
        float.TryParse(_recoverySpeed.Text, out var recoverySpeed);
        _worldSettings.DaySpeed = NormalizeDaySpeed(daySpeed);
        _worldSettings.RecoverFactor = NormalizeRecoverySpeed(recoverySpeed);
    }

    private void UpdateDescription()
    {
        if (_runServer.IsClicked)
        {
            SetDescription("RunServer");
        }
        else if (_needLogin.IsClicked)
        {
            SetDescription("NeedLogin");
        }
        else if (_randomSpawnPosition.IsClicked)
        {
            SetDescription("RandomSpawnPosition");
        }
        else if (_password.HasFocus)
        {
            SetDescription("Password");
        }
        else if (_maxPlayer.HasFocus)
        {
            SetDescription("MaxPlayers");
        }
        else if (_daySpeedTextBox.HasFocus)
        {
            SetDescription("DaySpeed");
        }
        else if (_recoverySpeed.HasFocus)
        {
            SetDescription("RecoverySpeed");
        }
        else if (_disableBlocks.HasFocus)
        {
            SetDescription("DisableBlocks");
        }
        else if (_keywordBlocking.HasFocus)
        {
            SetDescription("KeywordBlocking");
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
        return daySpeed <= 0f || daySpeed > 1f ? 1f : daySpeed;
    }

    private static float NormalizeRecoverySpeed(float recoverySpeed)
    {
        return recoverySpeed <= 0f ? 1f : recoverySpeed;
    }
}
