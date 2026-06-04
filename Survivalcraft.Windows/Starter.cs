using System.Reflection;
using System.Runtime.InteropServices;

using Engine.Core;
using Engine.Windowing;

using Game;

namespace Survivalcraft.Windows;

public class Starter
{
    public static void Main(string[] args)
    {
        var filteredArgs = RemoveServerFlags(args, out var runServer);
        if (runServer)
        {
            RunHeadlessServer(filteredArgs);
            return;
        }

        RunMode.Value = RunModeType.Gui;
        Window.IconStream = LoadWindowIcon();
        GameEntry.Main(filteredArgs);
    }

    /// <summary>
    /// 加载窗口图标
    /// </summary>
    private static Stream LoadWindowIcon()
    {
        var iconStream = typeof(Starter).GetTypeInfo().Assembly.GetManifestResourceStream("Starter.Resources.icon.png");
        return iconStream ?? throw new InvalidOperationException("Survivalcraft icon not found");
    }

    private static void RunHeadlessServer(string[] args)
    {
        RunMode.Value = RunModeType.HeadlessServer;
        AllocConsole();
        HeadlessEntry.Main(args);
    }

    private static string[] RemoveServerFlags(string[] args, out bool runServer)
    {
        runServer = false;
        var filteredArgs = new List<string>(args.Length);
        foreach (var arg in args)
        {
            if (string.Equals(arg, "-d", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--server", StringComparison.OrdinalIgnoreCase))
            {
                runServer = true;
                continue;
            }

            filteredArgs.Add(arg);
        }

        return filteredArgs.ToArray();
    }

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
}
