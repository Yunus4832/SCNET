using Microsoft.Win32;

namespace Game;

public static class GetMachineID
{
    public static string AndroidID = string.Empty;

    public static string GetAndroidID()
    {
        return AndroidID;
    }

    public static string GetMachineGuid()
    {
        try
        {
            const string location = @"SOFTWARE\Microsoft\Cryptography";
            const string name = "MachineGuid";
            using var localMachineX64View = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var rk = localMachineX64View.OpenSubKey(location);
            var machineGuid = rk?.GetValue(name);
            return machineGuid?.ToString() ?? string.Empty;
        }
        catch
        {
            return Guid.NewGuid().ToString();
        }
    }
}
