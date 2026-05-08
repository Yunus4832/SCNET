#if ANDROID
using Android.OS;

namespace Game.Managers;

public static class DeviceManager
{
    public static string DeviceModel => Build.Model ?? string.Empty;

    public static string OperatingSystemVersion => Build.VERSION.Release ?? string.Empty;
}
#endif
