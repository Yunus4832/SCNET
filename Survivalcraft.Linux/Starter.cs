using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Reflection;

using Engine.Core;
using Engine.FileStorage;
using Engine.Input;
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
        var instance = RegisterStorageRoots(args);
        TextInputManager.RegisterBackend(new SdlTextInputBackend());
        PlatformManager.RegisterPlatform(Platform.Desktop);
        PlatformManager.RegisterWebBrowserLauncher(OpenUrl);
        PlatformManager.RegisterInternetConnectionChecker(NetworkInterface.GetIsNetworkAvailable);
        PlatformManager.RegisterClipboard(ReadClipboardText, WriteClipboardText);
        var startup = StartupManager.Load(instance.GameArguments);
        var runningSetting = startup.Settings;
        InstallDesktopEntries();
        GameExitAction exitAction;
        if (runningSetting.RunMode is RunModeType.HeadlessServer)
        {
            RunHeadlessServer(startup);
            exitAction = GameExitManager.ExitAction;
        }
        else
        {
            RunMode.Value = RunModeType.Gui;
            Window.IconStream = LoadWindowIcon();
            PlatformManager.QueueLaunchUris(runningSetting.RemainingArgs);
            exitAction = GameEntry.EntryPoint(startup);
        }

        if (exitAction is GameExitAction.Restart)
        {
            var nextRunningSetting = RunningSettingManager.Load();
            RestartFromDesktop(nextRunningSetting.RunMode, instance.Id);
        }

        if (exitAction is GameExitAction.SwitchInstance)
        {
            var targetInstanceId = GameExitManager.SwitchInstanceId;
            RestartFromDesktop(StarterInstanceManager.GetRunMode(targetInstanceId), targetInstanceId);
        }
    }

    private static StarterInstanceContext RegisterStorageRoots(string[] args)
    {
        var appPath = AppContext.BaseDirectory;
        Storage.RegisterFileSystemRoot("app", appPath, readOnly: true);
        Storage.RegisterFileSystemRoot("starter", appPath);
        Storage.RegisterFileSystemRoot("system", Path.GetPathRoot(appPath) ?? appPath, allowEscapingRoot: true);
        var instance = StarterInstanceManager.Initialize(args);
        var instancePath = Storage.GetSystemPath(instance.InstancePath);
        Storage.RegisterFileSystemRoot("external", instancePath);
        Storage.RegisterFileSystemRoot("data", Path.Combine(instancePath, "Data"));
        Storage.RegisterFileSystemRoot("config", Path.Combine(instancePath, "Config"));
        return instance;
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

    private static void RunHeadlessServer(StartupContext startup)
    {
        RunMode.Value = RunModeType.HeadlessServer;
        HeadlessEntry.Main(startup);
    }

    private static string ReadClipboardText()
    {
        if (TryReadProcessOutput("wl-paste", ["--no-newline"], out var text) ||
            TryReadProcessOutput("xclip", ["-selection", "clipboard", "-out"], out text) ||
            TryReadProcessOutput("xsel", ["--clipboard", "--output"], out text))
        {
            return text;
        }

        Log.Warning("No supported Linux clipboard command found.");
        return string.Empty;
    }

    private static void WriteClipboardText(string text)
    {
        if (TryWriteProcessInput("wl-copy", [], text) ||
            TryWriteProcessInput("xclip", ["-selection", "clipboard"], text) ||
            TryWriteProcessInput("xsel", ["--clipboard", "--input"], text))
        {
            return;
        }

        throw new InvalidOperationException("No supported Linux clipboard command found.");
    }

    private static bool TryReadProcessOutput(string fileName, string[] arguments, out string output)
    {
        output = string.Empty;
        try
        {
            using var process = StartClipboardProcess(fileName, arguments, redirectInput: false);
            output = process.StandardOutput.ReadToEnd();
            return process.WaitForExit(1000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWriteProcessInput(string fileName, string[] arguments, string text)
    {
        try
        {
            using var process = StartClipboardProcess(fileName, arguments, redirectInput: true);
            process.StandardInput.Write(text);
            process.StandardInput.Close();
            return process.WaitForExit(1000) && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Process StartClipboardProcess(string fileName, string[] arguments, bool redirectInput)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardInput = redirectInput,
            RedirectStandardOutput = !redirectInput,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
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

    private static void RestartFromDesktop(RunModeType runMode, string instanceId)
    {
        var executablePath = Environment.ProcessPath
                             ?? throw new InvalidOperationException("Cannot determine executable path.");
        var executableDirectory = Path.GetDirectoryName(executablePath)
                                  ?? throw new InvalidOperationException("Cannot determine executable directory.");
        var desktopDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "applications");
        Directory.CreateDirectory(desktopDirectory);
        var desktopId = $"SurvivalcraftRestart{Environment.ProcessId}";
        var modeArgument = runMode is RunModeType.HeadlessServer ? "--server" : "--gui";
        WriteDesktopEntry(
            desktopDirectory,
            desktopId,
            "Survivalcraft Restart",
            executablePath,
            executableDirectory,
            $"{modeArgument} {StarterInstanceManager.InstanceArgument} {instanceId}",
            runMode is RunModeType.HeadlessServer,
            true);
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

    private static void OpenUrl(string url)
    {
        if (TryRunDesktopCommand("xdg-open", [url]))
        {
            return;
        }

        if (TryRunDesktopCommand("gio", ["open", url]))
        {
            return;
        }

        throw new InvalidOperationException("No supported desktop URL opener was found.");
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
