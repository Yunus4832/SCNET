using System.Xml.Linq;

namespace Game.Screens;

public class SettingsAudioScreen : Screen
{
    private readonly SliderWidget _musicVolumeSlider;

    private readonly SliderWidget _soundsVolumeSlider;

    public SettingsAudioScreen()
    {
        var node = ContentManager.Get<XElement>("Screens/SettingsAudioScreen");
        LoadContents(this, node);
        _soundsVolumeSlider = Children.Find<SliderWidget>("SoundsVolumeSlider")!;
        _musicVolumeSlider = Children.Find<SliderWidget>("MusicVolumeSlider")!;
    }

    public override void Update()
    {
        if (_soundsVolumeSlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.SoundsVolume = _soundsVolumeSlider.Value;
        }

        if (_musicVolumeSlider.IsSliding)
        {
            Configuration.ClientConfiguration.Preferences.MusicVolume = _musicVolumeSlider.Value;
        }

        _soundsVolumeSlider.Value = Configuration.ClientConfiguration.Preferences.SoundsVolume;
        _soundsVolumeSlider.Text =
            MathUtils.Round(Configuration.ClientConfiguration.Preferences.SoundsVolume * 10f).ToString();
        _musicVolumeSlider.Value = Configuration.ClientConfiguration.Preferences.MusicVolume;
        _musicVolumeSlider.Text = MathUtils.Round(Configuration.ClientConfiguration.Preferences.MusicVolume * 10f)
            .ToString();
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
