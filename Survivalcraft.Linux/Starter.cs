using System.Reflection;

using Engine.Core;
using Engine.Windowing;

using Game;

namespace Survivalcraft.Linux;

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
        // X11 Supported, Wayland not Supported
        Window.IconStream = LoadWindowIcon();
        // Generate Desktop file
        GenApplicationDesktopFile();
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

    /// <summary>
    /// 生成应用的 Desktop 文件
    /// </summary>
    public static void GenApplicationDesktopFile()
    {
        const string appId = "Survivalcraft";

        var exePath = Environment.ProcessPath
                      ?? throw new InvalidOperationException("Cannot determine executable path.");

        var exeDir = Path.GetDirectoryName(exePath)
                     ?? throw new InvalidOperationException("Cannot determine executable directory.");

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var iconDestDir = Path.Combine(homeDir, ".local", "share", "icons", "hicolor", "96x96", "apps");
        var iconDest = Path.Combine(iconDestDir, $"{appId}.png");

        if (!File.Exists(iconDest))
        {
            if (!Directory.Exists(iconDestDir))
            {
                Directory.CreateDirectory(iconDestDir);
            }

            using var fileStream = File.Create(iconDest);
            using var iconStream = LoadWindowIcon();
            iconStream.CopyTo(fileStream);
        }


        var desktopDir = Path.Combine(homeDir, ".local", "share", "applications");
        if (!Directory.Exists(desktopDir))
        {
            Directory.CreateDirectory(desktopDir);
        }

        var desktopFilePath = Path.Combine(desktopDir, $"{appId}.desktop");

        if (File.Exists(desktopFilePath))
        {
            return;
        }

        var content = $"""
                       [Desktop Entry]
                       Type=Application
                       Name=Survivalcraft
                       Comment=An infinite open world survival game
                       Icon={appId}
                       Exec={exePath}
                       Path={exeDir}
                       Terminal=false
                       Categories=Game;
                       StartupWMClass={appId}
                       """;

        File.WriteAllText(desktopFilePath, content);
    }

    private static void RunHeadlessServer(string[] args)
    {
        RunMode.Value = RunModeType.HeadlessServer;
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
}
