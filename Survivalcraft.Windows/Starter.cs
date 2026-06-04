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
        var runningSetting = RunningSettingManager.Load(args);
        if (runningSetting.RunMode is RunModeType.HeadlessServer)
        {
            RunHeadlessServer(runningSetting);
            return;
        }

        RunMode.Value = RunModeType.Gui;
        Window.IconStream = LoadWindowIcon();
        GameEntry.Main(runningSetting.RemainingArgs);
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

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
}
