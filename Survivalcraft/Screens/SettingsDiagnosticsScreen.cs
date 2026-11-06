using System.Xml.Linq;

using Engine.Network;

namespace Game.Screens;

public class SettingsDiagnosticsScreen : Screen
{
    private readonly ButtonWidget _displayDebugInfoButton;

    private readonly ButtonWidget _displayFpsRibbonButton;

    private readonly ButtonWidget _relayPreference;

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
        _relayPreference = Children.Find<ButtonWidget>("RelayPreference")!;
        _resetCompatibilityButton = Children.Find<ButtonWidget>("ResetCompatibilityButton")!;
    }

    public override void Update()
    {
        if (_relayPreference.IsClicked)
        {
            Configuration.HostingConfiguration.Preferences.InvitationRelayPolicy = (RelayPolicy)(
                ((int)Configuration.HostingConfiguration.Preferences.InvitationRelayPolicy + 1) %
                Enum.GetValues<RelayPolicy>().Length);
            SettingsManager.SaveSettings();
        }

        _relayPreference.Text = P2PRoomStatusDialog.Text(
            $"Relay{Configuration.HostingConfiguration.Preferences.InvitationRelayPolicy}"
        );
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
