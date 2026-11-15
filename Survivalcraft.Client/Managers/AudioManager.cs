using Engine.Audio;

namespace Game.Managers;

public static class AudioManager
{
    public static float MinAudibleVolume =>
        SoundPropagation.UnscaledMinAudibleVolume * Configuration.ClientConfiguration.Preferences.SoundsVolume;

    public static void PlaySound(
        string name,
        float volume,
        float pitch,
        float pan,
        CancellationToken cancellation = default
    )
    {
        if (!(Configuration.ClientConfiguration.Preferences.SoundsVolume > 0f))
        {
            return;
        }

        if (!(volume * Configuration.ClientConfiguration.Preferences.SoundsVolume > MinAudibleVolume))
        {
            return;
        }

        if (!Mixer.IsAudioInitialized || cancellation.IsCancellationRequested) { return; }

        var buffer = ContentManager.Get<SoundBuffer>(name);

        new Sound(
            buffer,
            volume * Configuration.ClientConfiguration.Preferences.SoundsVolume,
            ToEnginePitch(pitch),
            pan,
            false,
            true,
            cancellation
        ).Play();
    }

    public static float ToEnginePitch(float pitch)
    {
        return MathUtils.Pow(2f, pitch);
    }
}
