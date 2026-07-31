using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Engine.Core;
using Engine.FileStorage;
using Engine.Input;
using Engine.Windowing;

using Game;
using Game.ContentProviders;
using Game.Managers;

namespace Survivalcraft.Windows;

public class Starter
{
    public static void Main(string[] args)
    {
        RegisterStorageRoots();
        TextInputManager.RegisterBackend(new SdlTextInputBackend());
        PlatformManager.RegisterPlatform(Platform.Desktop);
        PlatformManager.RegisterWebBrowserLauncher(OpenUrl);
        PlatformManager.RegisterInternetConnectionChecker(NetworkInterface.GetIsNetworkAvailable);
        PlatformManager.RegisterClipboard(ReadClipboardText, WriteClipboardText);
        PlatformManager.RegisterExternalContentProviderFactory(() => new DiskExternalContentProvider());
        var runningSetting = RunningSettingManager.Load(args);
        if (runningSetting.RunMode is RunModeType.HeadlessServer)
        {
            RunHeadlessServer(runningSetting);
            if (GameExitManager.ExitAction is GameExitAction.Restart)
            {
                Restart([]);
            }

            return;
        }

        RunMode.Value = RunModeType.Gui;
        Window.IconStream = LoadWindowIcon();
        PlatformManager.QueueLaunchUris(runningSetting.RemainingArgs);
        if (GameEntry.EntryPoint(runningSetting) is GameExitAction.Restart)
        {
            Restart(args);
        }
    }

    private static void RegisterStorageRoots()
    {
        var appPath = AppContext.BaseDirectory;
        Storage.RegisterFileSystemRoot("app", appPath, readOnly: true);
        Storage.RegisterFileSystemRoot("external", appPath);
        Storage.RegisterFileSystemRoot("data", Path.Combine(appPath, "Data"));
        Storage.RegisterFileSystemRoot("config", Path.Combine(appPath, "Config"));
        Storage.RegisterFileSystemRoot("system", Path.GetPathRoot(appPath) ?? appPath, allowEscapingRoot: true);
    }

    /// <summary>
    /// 加载窗口图标
    /// </summary>
    private static Stream LoadWindowIcon()
    {
        var iconStream = typeof(Starter).GetTypeInfo().Assembly.GetManifestResourceStream("Starter.Resources.icon.png");
        return iconStream ?? throw new InvalidOperationException("Survivalcraft icon not found");
    }

    private static void RunHeadlessServer(RunningSetting runningSetting)
    {
        RunMode.Value = RunModeType.HeadlessServer;
        AllocConsole();
        HeadlessEntry.Main(runningSetting);
    }

    private static void Restart(string[] args)
    {
        var executablePath = Environment.ProcessPath
                             ?? throw new InvalidOperationException("Cannot determine executable path.");
        var startInfo = new ProcessStartInfo(executablePath) { UseShellExecute = false };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        Process.Start(startInfo);
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static string ReadClipboardText()
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            return string.Empty;
        }

        try
        {
            var handle = GetClipboardData(_cfUnicodeText);
            if (handle == IntPtr.Zero)
            {
                return string.Empty;
            }

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                return string.Empty;
            }

            try
            {
                return Marshal.PtrToStringUni(pointer) ?? string.Empty;
            }
            finally
            {
                GlobalUnlock(handle);
            }
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static void WriteClipboardText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
        {
            throw new InvalidOperationException("Could not open clipboard.");
        }

        var handle = IntPtr.Zero;
        try
        {
            EmptyClipboard();
            var bytes = Encoding.Unicode.GetBytes(text + '\0');
            handle = GlobalAlloc(_gMemMoveable, (UIntPtr)bytes.Length);
            if (handle == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not allocate clipboard memory.");
            }

            var pointer = GlobalLock(handle);
            if (pointer == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not lock clipboard memory.");
            }

            try
            {
                Marshal.Copy(bytes, 0, pointer, bytes.Length);
            }
            finally
            {
                GlobalUnlock(handle);
            }

            if (SetClipboardData(_cfUnicodeText, handle) == IntPtr.Zero)
            {
                throw new InvalidOperationException("Could not set clipboard data.");
            }

            handle = IntPtr.Zero;
        }
        finally
        {
            if (handle != IntPtr.Zero)
            {
                GlobalFree(handle);
            }

            CloseClipboard();
        }
    }

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    private const uint _cfUnicodeText = 13;

    private const uint _gMemMoveable = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint uFormat);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);
}
