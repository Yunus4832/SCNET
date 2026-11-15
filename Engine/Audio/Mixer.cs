using Engine.Core.Mathematics;

using Silk.NET.OpenAL;

namespace Engine.Audio;

/// <summary>Application audio lifetime and the entry point to its dedicated device owner.</summary>
public static class Mixer
{
    private static readonly Lock _lifetime = new();

    private static AudioService? _service;

    private static float _volume = 1f;

    internal static AudioService? Service => Volatile.Read(ref _service);

    internal static AL? AL => Service?.Api;

    public static bool IsAudioInitialized => Service?.IsReady == true;

    public static float MasterVolume
    {
        get => Volatile.Read(ref _volume);
        set
        {
            Volatile.Write(ref _volume, MathUtils.Saturate(value));
            Service?.Wake();
        }
    }

    public static void Initialize()
    {
        lock (_lifetime)
        {
            if (_service != null) { return; }

            var service = new AudioService();
            _service = service;
            service.Start();
        }
    }

    internal static void Dispose()
    {
        lock (_lifetime)
        {
            var service = Interlocked.Exchange(ref _service, null);
            service?.Dispose();
        }
    }

    internal static void DisposeSounds() => Service?.DisposeSounds();

    public static void RequestDeviceRefresh() => Service?.Refresh();
}
