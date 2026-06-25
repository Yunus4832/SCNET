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
            SettingsManager.Current.MultithreadedTerrainUpdate = !SettingsManager.Current.MultithreadedTerrainUpdate;
            _descriptionLabel.Text =
                StringsManager.GetString("Settings.Compatibility.SingleThreadTerrainUpdate.Description");
        }

        if (_useReducedZRangeButton.IsClicked)
        {
            SettingsManager.Current.UseReducedZRange = !SettingsManager.Current.UseReducedZRange;
            _descriptionLabel.Text = StringsManager.GetString("Settings.Compatibility.UseReducedZRange.Description");
        }

        if (_enableModButton.IsClicked)
        {
            SettingsManager.Current.EnableMod = !SettingsManager.Current.EnableMod;
        }

        if (_viewGameLogButton.IsClicked)
        {
            DialogsManager.ShowDialog(null, new ViewGameLogDialog());
        }

        if (_resetDefaultsButton.IsClicked)
        {
            SettingsManager.Current.MultithreadedTerrainUpdate = true;
            SettingsManager.Current.UseReducedZRange = false;
        }

        _singleThreadTerrainUpdateButton.Text =
            SettingsManager.Current.MultithreadedTerrainUpdate ? LanguageManager.Off : LanguageManager.On;

        _useReducedZRangeButton.Text = SettingsManager.Current.UseReducedZRange ? LanguageManager.On : LanguageManager.Off;

        _enableModButton.Text = SettingsManager.Current.EnableMod ? LanguageManager.On : LanguageManager.Off;

        _resetDefaultsButton.IsEnabled =
            !SettingsManager.Current.MultithreadedTerrainUpdate || SettingsManager.Current.UseReducedZRange;

        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
