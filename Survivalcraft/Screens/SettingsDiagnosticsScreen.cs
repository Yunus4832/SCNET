using System.Xml.Linq;

using Engine.Network;

namespace Game.Screens;

public class SettingsDiagnosticsScreen : Screen
{
    private readonly ButtonWidget _displayDebugInfoButton;

    private readonly ButtonWidget _displayFpsRibbonButton;

    private readonly InlineSelectionWidget _relayPreferenceSelection;

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
        _relayPreferenceSelection = Children.Find<InlineSelectionWidget>("RelayPreference")!;
        _relayPreferenceSelection.ConfigureEnum<RelayPolicy>(
            item => P2PRoomStatusDialog.Text($"Relay{item}"),
            policy =>
            {
                Configuration.HostingConfiguration.Preferences.InvitationRelayPolicy = policy;
                SettingsManager.SaveSettings();
            }
        );
        _resetCompatibilityButton = Children.Find<ButtonWidget>("ResetCompatibilityButton")!;
    }

    public override void Leave()
    {
        _relayPreferenceSelection.Close();
    }

    public override void Update()
    {
        if (_displayDebugInfoButton.IsClicked)
        {
            DebugOverlayManager.ToggleVisibility();
        }

        if (_displayFpsRibbonButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.DisplayFpsRibbon =
                !Configuration.ClientConfiguration.Preferences.DisplayFpsRibbon;
        }

        if (_viewGameLogButton.IsClicked)
        {
            DialogsManager.ShowDialog(null, new ViewGameLogDialog());
        }

        if (_useReducedZRangeButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.UseReducedZRange =
                !Configuration.ClientConfiguration.Preferences.UseReducedZRange;
        }

        if (_resetCompatibilityButton.IsClicked)
        {
            Configuration.ClientConfiguration.Preferences.UseReducedZRange = false;
            Configuration.HostingConfiguration.Preferences.InvitationRelayPolicy = RelayPolicy.Allowed;
            SettingsManager.SaveSettings();
        }

        _relayPreferenceSelection.SelectedItem = Configuration.HostingConfiguration.Preferences.InvitationRelayPolicy;
        _displayDebugInfoButton.Text = Configuration.ClientConfiguration.Preferences.DisplayDebugInfo
            ? Localization.ClientLanguage.Catalog.Yes
            : Localization.ClientLanguage.Catalog.No;
        _displayFpsRibbonButton.Text = Configuration.ClientConfiguration.Preferences.DisplayFpsRibbon
            ? Localization.ClientLanguage.Catalog.Yes
            : Localization.ClientLanguage.Catalog.No;
        _useReducedZRangeButton.Text = Configuration.ClientConfiguration.Preferences.UseReducedZRange
            ? Localization.ClientLanguage.Catalog.On
            : Localization.ClientLanguage.Catalog.Off;
        _resetCompatibilityButton.IsEnabled = Configuration.ClientConfiguration.Preferences.UseReducedZRange ||
                                              Configuration.HostingConfiguration.Preferences
                                                  .InvitationRelayPolicy !=
                                              RelayPolicy.Allowed;

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
