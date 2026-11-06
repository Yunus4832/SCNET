using System.Globalization;
using System.Xml.Linq;

namespace Game.Screens;

public class SettingsGraphicsScreen : Screen
{
    private readonly SliderWidget _viewAngleSlider;

    private readonly SliderWidget _brightnessSlider;

    private readonly BevelledButtonWidget _virtualRealityButton;

    private readonly ContainerWidget _vrPanel;

    public SettingsGraphicsScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/SettingsGraphicsScreen");
        LoadContents(this, node);
        _virtualRealityButton = Children.Find<BevelledButtonWidget>("VirtualRealityButton")!;
        _brightnessSlider = Children.Find<SliderWidget>("BrightnessSlider")!;
        _viewAngleSlider = Children.Find<SliderWidget>("ViewAngleSlider")!;
        _vrPanel = Children.Find<ContainerWidget>("VrPanel")!;
        _vrPanel.IsVisible = false;
    }

    public override void Update()
    {
        if (_viewAngleSlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.ViewAngle = _viewAngleSlider.Value;
        }

        if (_virtualRealityButton.IsClicked)
        {
            if (Configuration.ClientConfiguration.Preferences.UseVr)
            {
                Configuration.ClientConfiguration.Preferences.UseVr = false;
                VrManager.StopVr();
            }
            else
            {
                Configuration.ClientConfiguration.Preferences.UseVr = true;
                VrManager.StartVr();
            }
        }

        if (_brightnessSlider.IsSliding)
        {
            Configuration.ClientConfiguration.SetBrightness(_brightnessSlider.Value);
        }

        _virtualRealityButton.IsEnabled = VrManager.IsVrAvailable;
        _virtualRealityButton.Text = Configuration.ClientConfiguration.Preferences.UseVr
            ? Localization.ClientLanguage.Catalog.On
            : Localization.ClientLanguage.Catalog.Off;
        _brightnessSlider.Value = Configuration.ClientConfiguration.Preferences.Brightness;
        _brightnessSlider.Text =
            MathUtils.Round(Configuration.ClientConfiguration.Preferences.Brightness * 10f)
                .ToString(CultureInfo.InvariantCulture);
        _viewAngleSlider.Value = Configuration.ClientConfiguration.Preferences.ViewAngle;
        _viewAngleSlider.Text =
            $"{MathUtils.Round(Configuration.ClientConfiguration.Preferences.ViewAngle * 100f)}%";
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
