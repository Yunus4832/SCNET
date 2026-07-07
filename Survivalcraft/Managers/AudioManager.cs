using Engine.Audio;

namespace Game.Managers;

public static class AudioManager
{
    private static readonly Dictionary<string, SoundBuffer> _bufferCaches = new();

    public static float MinAudibleVolume => 0.05f * SettingsManager.Current.SoundsVolume;

    public static void PlaySound(string name, float volume, float pitch, float pan)
    {
        if (RunMode.Value is RunModeType.HeadlessServer)
        {
            return;
        }

        if (!(SettingsManager.Current.SoundsVolume > 0f))
        {
            return;
        }

        if (!(volume * SettingsManager.Current.SoundsVolume > MinAudibleVolume))
        {
            return;
        }

        if (!_bufferCaches.TryGetValue(name, out var buffer))
        {
            buffer = ContentManager.Get<SoundBuffer>(name);
            _bufferCaches.Add(name, buffer);
        }

        new Sound(buffer, volume * SettingsManager.Current.SoundsVolume, ToEnginePitch(pitch), pan, false, true).Play();
    }

    public static void Initialize()
    {
        _bufferCaches.Clear();
    }

    public static float ToEnginePitch(float pitch)
    {
        return MathUtils.Pow(2f, pitch);
    }
}
