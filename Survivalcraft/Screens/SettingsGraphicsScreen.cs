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
        GameManager.UpdateProject();
        if (_viewAngleSlider.IsSliding)
        {
            SettingsManager.ViewAngle = _viewAngleSlider.Value;
        }

        if (_virtualRealityButton.IsClicked)
        {
            if (SettingsManager.UseVr)
            {
                SettingsManager.UseVr = false;
                VrManager.StopVr();
            }
            else
            {
                SettingsManager.UseVr = true;
                VrManager.StartVr();
            }
        }

        if (_brightnessSlider.IsSliding)
        {
            SettingsManager.Brightness = _brightnessSlider.Value;
        }

        _virtualRealityButton.IsEnabled = VrManager.IsVrAvailable;
        _virtualRealityButton.Text = SettingsManager.UseVr ? "Enabled" : "Disabled";
        _brightnessSlider.Value = SettingsManager.Brightness;
        _brightnessSlider.Text =
            MathUtils.Round(SettingsManager.Brightness * 10f).ToString(CultureInfo.InvariantCulture);
        _viewAngleSlider.Value = SettingsManager.ViewAngle;
        _viewAngleSlider.Text = $"{MathUtils.Round(SettingsManager.ViewAngle * 100f)}%";
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
