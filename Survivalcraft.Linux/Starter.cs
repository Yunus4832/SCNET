using System.Diagnostics;
using System.Reflection;

using Engine.Core;
using Engine.Windowing;

using Game;
using Game.Managers;

namespace Survivalcraft.Linux;

public class Starter
{
    private const string _appId = "Survivalcraft";
    private const string _serverAppId = "SurvivalcraftServer";

    public static void Main(string[] args)
    {
        var runningSetting = RunningSettingManager.Load(args);
        InstallDesktopEntries();

        var exitAction = GameExitAction.Exit;
        if (runningSetting.RunMode is RunModeType.HeadlessServer)
        {
            RunHeadlessServer(runningSetting);
        }
        else
        {
            RunMode.Value = RunModeType.Gui;
            // Wayland is supported; window icon settings may not take effect there.
            Window.IconStream = LoadWindowIcon();
            exitAction = GameEntry.Main(runningSetting.RemainingArgs);
        }

        var nextRunningSetting = RunningSettingManager.Load([]);
        if (exitAction is GameExitAction.Restart)
        {
            RestartFromDesktop(nextRunningSetting.RunMode);
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

    private static void InstallDesktopEntries()
    {
        var exePath = Environment.ProcessPath
                      ?? throw new InvalidOperationException("Cannot determine executable path.");

        var exeDir = Path.GetDirectoryName(exePath)
                     ?? throw new InvalidOperationException("Cannot determine executable directory.");

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var iconDestDir = Path.Combine(homeDir, ".local", "share", "icons", "hicolor", "96x96", "apps");
        var iconDest = Path.Combine(iconDestDir, $"{_appId}.png");

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

        WriteDesktopEntry(desktopDir, _appId, "Survivalcraft", exePath, exeDir, "--gui", false, false);
        WriteDesktopEntry(
            desktopDir,
            _serverAppId,
            "Survivalcraft Server",
            exePath,
            exeDir,
            "--server",
            true,
            true);
    }

    private static void RunHeadlessServer(RunningSetting runningSetting)
    {
        RunMode.Value = RunModeType.HeadlessServer;
        HeadlessEntry.Main(runningSetting);
    }

    private static void WriteDesktopEntry(
        string desktopDir,
        string desktopId,
        string name,
        string executablePath,
        string workingDirectory,
        string argument,
        bool terminal,
        bool noDisplay)
    {
        var desktopFilePath = Path.Combine(desktopDir, $"{desktopId}.desktop");
        if (File.Exists(desktopFilePath))
        {
            return;
        }

        var escapedExecutablePath = executablePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        var content = $"""
                       [Desktop Entry]
                       Type=Application
                       Name={name}
                       Comment=An infinite open world survival game
                       Icon={_appId}
                       Exec="{escapedExecutablePath}" {argument}
                       Path={workingDirectory}
                       Terminal={(terminal ? "true" : "false")}
                       NoDisplay={(noDisplay ? "true" : "false")}
                       Categories=Game;
                       StartupWMClass={_appId}
                       """;

        File.WriteAllText(desktopFilePath, content);
    }

    private static void RestartFromDesktop(RunModeType runMode)
    {
        var desktopId = runMode is RunModeType.HeadlessServer ? _serverAppId : _appId;
        if (TryLaunchDesktopEntry("gtk-launch", [desktopId]))
        {
            return;
        }

        TryRunDesktopCommand("kbuildsycoca6", ["--noincremental"]);
        if (TryLaunchDesktopEntry("kioclient6", ["exec", $"applications:{desktopId}.desktop"]))
        {
            return;
        }

        TryRunDesktopCommand("kbuildsycoca5", ["--noincremental"]);
        if (TryLaunchDesktopEntry("kioclient5", ["exec", $"applications:{desktopId}.desktop"]))
        {
            return;
        }

        NotifyManualRestartRequired();
    }

    private static bool TryLaunchDesktopEntry(string executable, string[] arguments)
    {
        try
        {
            var executablePath = FindExecutable(executable);
            var setsidPath = FindExecutable("setsid");
            if (executablePath is null || setsidPath is null)
            {
                return false;
            }

            var startInfo = new ProcessStartInfo(setsidPath) { UseShellExecute = false };
            startInfo.ArgumentList.Add("--fork");
            startInfo.ArgumentList.Add(executablePath);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            return Process.Start(startInfo) is not null;
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to launch desktop entry with {executable}: {ex.Message}");
            return false;
        }
    }

    private static string? FindExecutable(string executable)
    {
        if (Path.IsPathRooted(executable))
        {
            return File.Exists(executable) ? executable : null;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool TryRunDesktopCommand(string executable, string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            var process = Process.Start(startInfo);
            return process is not null && process.WaitForExit(10000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void NotifyManualRestartRequired()
    {
        try
        {
            var startInfo = new ProcessStartInfo("notify-send")
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("--app-name=Survivalcraft");
            startInfo.ArgumentList.Add("--icon=Survivalcraft");
            startInfo.ArgumentList.Add("Survivalcraft 需要重新启动");
            startInfo.ArgumentList.Add("运行模式已更新，请通过应用图标手动重新启动。");

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Log.Warning($"Failed to show restart notification: {ex.Message}");
        }
    }
}
