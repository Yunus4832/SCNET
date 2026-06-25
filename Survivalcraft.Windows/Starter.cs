using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

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

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
}
