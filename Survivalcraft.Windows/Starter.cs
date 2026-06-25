using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Engine.Core;
using Engine.Windowing;

using Game;
using Game.Managers;

namespace Survivalcraft.Windows;

public class Starter
{
    public static void Main(string[] args)
    {
        WebBrowserManager.RegisterLauncher(OpenUrl);
        WebManager.RegisterInternetConnectionChecker(NetworkInterface.GetIsNetworkAvailable);
        ClipboardManager.RegisterClipboard(ReadClipboardText, WriteClipboardText);
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
        if (GameEntry.Main(runningSetting) is GameExitAction.Restart)
        {
            Restart(args);
        }
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
            var handle = GetClipboardData(CF_UNICODETEXT);
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
            handle = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
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

            if (SetClipboardData(CF_UNICODETEXT, handle) == IntPtr.Zero)
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

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

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
