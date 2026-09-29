using System.Xml.Linq;

namespace Game.Screens;

public class SettingsScreen : Screen
{
    private readonly ButtonWidget _audioButton;

    private readonly ButtonWidget _controlsButton;

    private readonly ButtonWidget _diagnosticsButton;

    private readonly ButtonWidget _graphicsButton;

    private readonly ButtonWidget _performanceButton;

    private Screen? _previousScreen;

    private readonly ButtonWidget _uiButton;

    public SettingsScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/SettingsScreen");
        LoadContents(this, node);
        _performanceButton = Children.Find<ButtonWidget>("Performance")!;
        _graphicsButton = Children.Find<ButtonWidget>("Graphics")!;
        _uiButton = Children.Find<ButtonWidget>("Ui")!;
        _diagnosticsButton = Children.Find<ButtonWidget>("Diagnostics")!;
        _audioButton = Children.Find<ButtonWidget>("Audio")!;
        _controlsButton = Children.Find<ButtonWidget>("Controls")!;
    }

    public override void Enter(object[] parameters)
    {
        _previousScreen ??= ScreensManager.PreviousScreen;
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (_performanceButton.IsClicked)
        {
            ScreensManager.SwitchScreen("SettingsPerformance");
        }

        if (_graphicsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("SettingsGraphics");
        }

        if (_uiButton.IsClicked)
        {
            ScreensManager.SwitchScreen("SettingsUi");
        }

        if (_diagnosticsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("SettingsDiagnostics");
        }

        if (_audioButton.IsClicked)
        {
            ScreensManager.SwitchScreen("SettingsAudio");
        }

        if (_controlsButton.IsClicked)
        {
            ScreensManager.SwitchScreen("SettingsControls");
        }

        if (Input is { Back: false, Cancel: false } && !Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            return;
        }

        SettingsManager.SaveSettings();
        ScreensManager.SwitchScreen(_previousScreen);
        _previousScreen = null;
    }
}
