using System.Xml.Linq;

namespace Game.Screens;

public class SettingsCompatibilityScreen : Screen
{
    private readonly LabelWidget _descriptionLabel;

    private readonly ButtonWidget _enableModButton;

    private readonly ButtonWidget _resetDefaultsButton;

    private readonly ButtonWidget _singleThreadTerrainUpdateButton;

    private readonly ButtonWidget _useReducedZRangeButton;

    private readonly ContainerWidget _useReducedZRangeContainer;

    private readonly ButtonWidget _viewGameLogButton;

    public SettingsCompatibilityScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/SettingsCompatibilityScreen");
        LoadContents(this, node);
        _singleThreadTerrainUpdateButton = Children.Find<ButtonWidget>("SingleThreadTerrainUpdateButton")!;
        _useReducedZRangeButton = Children.Find<ButtonWidget>("UseReducedZRangeButton")!;
        _enableModButton = Children.Find<ButtonWidget>("EnableMod")!;
        _useReducedZRangeContainer = Children.Find<ContainerWidget>("UseReducedZRangeContainer")!;
        _viewGameLogButton = Children.Find<ButtonWidget>("ViewGameLogButton")!;
        _resetDefaultsButton = Children.Find<ButtonWidget>("ResetDefaultsButton")!;
        _descriptionLabel = Children.Find<LabelWidget>("Description")!;
    }

    public override void Enter(object[] parameters)
    {
        _descriptionLabel.Text = string.Empty;
#if ANDROID
        _useReducedZRangeContainer.IsVisible = true;
#endif
#if DESKTOP
        _useReducedZRangeContainer.IsVisible = false;
#endif
    }

    public override void Update()
    {
        GameManager.UpdateProject();
        if (_singleThreadTerrainUpdateButton.IsClicked)
        {
            SettingsManager.MultithreadedTerrainUpdate = !SettingsManager.MultithreadedTerrainUpdate;
            _descriptionLabel.Text =
                StringsManager.GetString("Settings.Compatibility.SingleThreadTerrainUpdate.Description");
        }

        if (_useReducedZRangeButton.IsClicked)
        {
            SettingsManager.UseReducedZRange = !SettingsManager.UseReducedZRange;
            _descriptionLabel.Text = StringsManager.GetString("Settings.Compatibility.UseReducedZRange.Description");
        }

        if (_enableModButton.IsClicked)
        {
            SettingsManager.EnableMod = !SettingsManager.EnableMod;
        }

        if (_viewGameLogButton.IsClicked)
        {
            DialogsManager.ShowDialog(null, new ViewGameLogDialog());
        }

        if (_resetDefaultsButton.IsClicked)
        {
            SettingsManager.MultithreadedTerrainUpdate = true;
            SettingsManager.UseReducedZRange = false;
        }

        _singleThreadTerrainUpdateButton.Text =
            SettingsManager.MultithreadedTerrainUpdate ? LanguageControl.Off : LanguageControl.On;

        _useReducedZRangeButton.Text = SettingsManager.UseReducedZRange ? LanguageControl.On : LanguageControl.Off;

        _enableModButton.Text = SettingsManager.EnableMod ? LanguageControl.On : LanguageControl.Off;

        _resetDefaultsButton.IsEnabled =
            !SettingsManager.MultithreadedTerrainUpdate || SettingsManager.UseReducedZRange;

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
