using System.Xml.Linq;

namespace Game.Screens;

public class SettingsDiagnosticsScreen : Screen
{
    private readonly ButtonWidget _displayDebugInfoButton;

    private readonly ButtonWidget _displayFpsRibbonButton;

    private readonly ButtonWidget _resetCompatibilityButton;

    private readonly ButtonWidget _useReducedZRangeButton;

    private readonly ButtonWidget _viewGameLogButton;

    public SettingsDiagnosticsScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/SettingsDiagnosticsScreen");
        LoadContents(this, node);
        _displayDebugInfoButton = Children.Find<ButtonWidget>("DisplayDebugInfoButton")!;
        _displayFpsRibbonButton = Children.Find<ButtonWidget>("DisplayFpsRibbonButton")!;
        _viewGameLogButton = Children.Find<ButtonWidget>("ViewGameLogButton")!;
        _useReducedZRangeButton = Children.Find<ButtonWidget>("UseReducedZRangeButton")!;
        _resetCompatibilityButton = Children.Find<ButtonWidget>("ResetCompatibilityButton")!;
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (_displayDebugInfoButton.IsClicked)
        {
            DebugOverlayManager.ToggleVisibility();
        }

        if (_displayFpsRibbonButton.IsClicked)
        {
            SettingsManager.Current.DisplayFpsRibbon = !SettingsManager.Current.DisplayFpsRibbon;
        }

        if (_viewGameLogButton.IsClicked)
        {
            DialogsManager.ShowDialog(null, new ViewGameLogDialog());
        }

        if (_useReducedZRangeButton.IsClicked)
        {
            SettingsManager.Current.UseReducedZRange = !SettingsManager.Current.UseReducedZRange;
        }

        if (_resetCompatibilityButton.IsClicked)
        {
            SettingsManager.Current.UseReducedZRange = false;
        }

        _displayDebugInfoButton.Text = SettingsManager.Current.DisplayDebugInfo
            ? LanguageManager.Yes
            : LanguageManager.No;
        _displayFpsRibbonButton.Text = SettingsManager.Current.DisplayFpsRibbon
            ? LanguageManager.Yes
            : LanguageManager.No;
        _useReducedZRangeButton.Text = SettingsManager.Current.UseReducedZRange
            ? LanguageManager.On
            : LanguageManager.Off;
        _resetCompatibilityButton.IsEnabled = SettingsManager.Current.UseReducedZRange;

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
