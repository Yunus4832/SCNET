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
        GameManager.UpdateProject();
        if (_soundsVolumeSlider.IsSliding)
        {
            SettingsManager.SoundsVolume = _soundsVolumeSlider.Value;
        }

        if (_musicVolumeSlider.IsSliding)
        {
            SettingsManager.MusicVolume = _musicVolumeSlider.Value;
        }

        _soundsVolumeSlider.Value = SettingsManager.SoundsVolume;
        _soundsVolumeSlider.Text = MathUtils.Round(SettingsManager.SoundsVolume * 10f).ToString();
        _musicVolumeSlider.Value = SettingsManager.MusicVolume;
        _musicVolumeSlider.Text = MathUtils.Round(SettingsManager.MusicVolume * 10f).ToString();
        if (Input.Back || Input.Cancel || Children.Find<ButtonWidget>("TopBar.Back")!.IsClicked)
        {
            SettingsManager.SaveSettings();
            ScreensManager.SwitchScreen(ScreensManager.PreviousScreen);
        }
    }
}
