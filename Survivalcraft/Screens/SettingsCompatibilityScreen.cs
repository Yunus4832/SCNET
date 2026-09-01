using System.Xml.Linq;

namespace Game.Screens;

public class SettingsCompatibilityScreen : Screen
{
    private readonly ButtonWidget _resetDefaultsButton;

    private readonly ButtonWidget _useReducedZRangeButton;

    private readonly ButtonWidget _viewGameLogButton;

    public SettingsCompatibilityScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/SettingsCompatibilityScreen");
        LoadContents(this, node);
        _resetDefaultsButton = Children.Find<ButtonWidget>("ResetDefaultsButton")!;
        _useReducedZRangeButton = Children.Find<ButtonWidget>("UseReducedZRangeButton")!;
        _viewGameLogButton = Children.Find<ButtonWidget>("ViewGameLogButton")!;
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (_useReducedZRangeButton.IsClicked)
        {
            SettingsManager.Current.UseReducedZRange = !SettingsManager.Current.UseReducedZRange;
        }

        if (_viewGameLogButton.IsClicked)
        {
            DialogsManager.ShowDialog(null, new ViewGameLogDialog());
        }

        if (_resetDefaultsButton.IsClicked)
        {
            SettingsManager.Current.UseReducedZRange = false;
        }

        _useReducedZRangeButton.Text =
            SettingsManager.Current.UseReducedZRange ? LanguageManager.On : LanguageManager.Off;
        _resetDefaultsButton.IsEnabled = SettingsManager.Current.UseReducedZRange;

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
