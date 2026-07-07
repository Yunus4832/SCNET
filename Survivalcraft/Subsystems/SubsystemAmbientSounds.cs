using Engine.Audio;

using EntitySystem.Core;
using EntitySystem.TemplatesDatabase;

namespace Game.Subsystems;

public class SubsystemAmbientSounds : Subsystem, IUpdateable
{
    private Sound _fireSound = null!;

    private Sound _magmaSound = null!;

    private readonly Random _random = new();

    private Sound _waterSound = null!;

    public SubsystemAudio SubsystemAudio { get; set; } = null!;

    public float FireSoundVolume { get; set; }

    public float WaterSoundVolume { get; set; }

    public float MagmaSoundVolume { get; set; }

    public UpdateOrder UpdateOrder => UpdateOrder.Default;

    public void Update(float dt)
    {
        _fireSound.Volume = MathUtils.Lerp(_fireSound.Volume, SettingsManager.Current.SoundsVolume * FireSoundVolume,
            MathUtils.Saturate(3f * Time.FrameDuration));
        if (_fireSound.Volume > 0.5f * AudioManager.MinAudibleVolume)
        {
            _fireSound.Play();
        }
        else
        {
            _fireSound.Pause();
        }

        _waterSound.Volume = MathUtils.Lerp(_waterSound.Volume, SettingsManager.Current.SoundsVolume * WaterSoundVolume,
            MathUtils.Saturate(3f * Time.FrameDuration));
        if (_waterSound.Volume > 0.5f * AudioManager.MinAudibleVolume)
        {
            _waterSound.Play();
        }
        else
        {
            _waterSound.Pause();
        }

        _magmaSound.Volume = MathUtils.Lerp(_magmaSound.Volume, SettingsManager.Current.SoundsVolume * MagmaSoundVolume,
            MathUtils.Saturate(3f * Time.FrameDuration));
        if (_magmaSound.Volume > 0.5f * AudioManager.MinAudibleVolume)
        {
            _magmaSound.Play();
        }
        else
        {
            _magmaSound.Pause();
        }

        if (_magmaSound.State == SoundState.Playing && _random.Bool(0.2f * dt))
        {
            SubsystemAudio.PlayRandomSound("Audio/Sizzles", _magmaSound.Volume, _random.Float(-0.2f, 0.2f), 0f, 0f);
        }

        FireSoundVolume = 0f;
        WaterSoundVolume = 0f;
        MagmaSoundVolume = 0f;
    }

    public override void Load(ValuesDictionary valuesDictionary)
    {
        SubsystemAudio = Project.FindSubsystem<SubsystemAudio>(true)!;
        _fireSound = SubsystemAudio.CreateSound("Audio/Fire");
        _fireSound.IsLooped = true;
        _fireSound.Volume = 0f;
        _waterSound = SubsystemAudio.CreateSound("Audio/Water");
        _waterSound.IsLooped = true;
        _waterSound.Volume = 0f;
        _magmaSound = SubsystemAudio.CreateSound("Audio/Magma");
        _magmaSound.IsLooped = true;
        _magmaSound.Volume = 0f;
    }
}
