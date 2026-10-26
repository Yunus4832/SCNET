using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

using Engine.Core.Runtime;
using Engine.FileStorage;
using Engine.Input;
using Engine.Windowing;

using Game;
using Game.Managers;
using Game.Platforms;
using Game.Startup;

using Scnet.Desktop.Updates;

namespace Survivalcraft.Windows;

public class Starter
{
    public static void Main(string[] args)
    {
        if (DesktopUpdateCoordinator.HandleStartup(
                args,
                AppContext.BaseDirectory,
                "windows-x64",
                DesktopUpdateFiles.Replace,
                DesktopUpdateFiles.Notify
            ))
        {
            return;
        }

        var instance = RegisterStorageRoots(args);
        PlatformManager.RegisterPlatform(Platform.Desktop);
        PlatformManager.RegisterWebBrowserLauncher(OpenUrl);
        PlatformManager.RegisterInstanceLauncher(LaunchInstance);
        var startup = StartupManager.Load(instance.GameArguments);
        var runningSetting = startup.Settings;
        if (runningSetting.RunMode is RunModeType.HeadlessServer)
        {
            RunHeadlessServer(startup);
            if (GameExitManager.ExitAction is GameExitAction.Restart or GameExitAction.SwitchInstance)
            {
                Restart(
                    GameExitManager.ExitAction is GameExitAction.SwitchInstance
                        ? GameExitManager.SwitchInstanceId
                        : instance.Id
                );
            }

            return;
        }

        PlatformManager.RegisterUpdateSupport(
            Path.Combine(Path.GetTempPath(), "scnet-updates"),
            ".zip",
            "ConfirmDesktopInstall"
        );
        RunMode.Value = RunModeType.Gui;
        PlatformManager.RegisterTextInput(new SdlTextInputBackend());
        PlatformManager.RegisterClipboard(new SdlClipboardBackend());
        PlatformManager.RegisterFilePicker(new WindowsFilePicker());
        Window.IconStream = LoadWindowIcon();
        PlatformManager.QueueLaunchUris(runningSetting.RemainingArgs);
        var exitAction = GameEntry.EntryPoint(startup);
        if (exitAction is GameExitAction.Update)
        {
            InstallDesktopUpdate(GameExitManager.UpdatePackagePath);
            return;
        }

        if (exitAction is GameExitAction.Restart or GameExitAction.SwitchInstance)
        {
            Restart(
                exitAction is GameExitAction.SwitchInstance
                    ? GameExitManager.SwitchInstanceId
                    : instance.Id
            );
        }
    }

    private static void InstallDesktopUpdate(string path)
    {
        try
        {
            var task = DesktopUpdateCoordinator.Prepare(path, AppContext.BaseDirectory, "windows-x64");
            DesktopUpdateCoordinator.Launch(task);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Could not start desktop update: {exception}");
            DesktopUpdateFiles.Notify(
                $"{LanguageManager.GetContentWidgets("GameInformationManager", "DesktopInstallFailed")}\n{GameExitManager.UpdateDownloadUrl}\n{exception.Message}",
                true
            );
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Could not remove downloaded update: {exception.Message}");
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
    ///     加载窗口图标
    /// </summary>
    private static Stream LoadWindowIcon()
    {
        var iconStream = typeof(Starter).GetTypeInfo().Assembly.GetManifestResourceStream("Starter.Resources.icon.png");
        return iconStream ?? throw new InvalidOperationException("Survivalcraft icon not found");
    }

    private static void RunHeadlessServer(StartupContext startup)
    {
        RunMode.Value = RunModeType.HeadlessServer;
        AllocConsole();
        Environment.ExitCode = HeadlessEntry.Main(startup);
    }

    private static void Restart(string instanceId)
    {
        StartInstanceProcess(instanceId, false);
    }

    private static void LaunchInstance(string instanceId)
    {
        StartInstanceProcess(instanceId, true);
    }

    private static void StartInstanceProcess(string instanceId, bool forceGui)
    {
        var executablePath = Environment.ProcessPath ??
                             throw new InvalidOperationException("Cannot determine executable path.");
        var startInfo = new ProcessStartInfo(executablePath) { UseShellExecute = false };
        if (forceGui)
        {
            startInfo.ArgumentList.Add("--gui");
        }

        startInfo.ArgumentList.Add(StarterInstanceManager.InstanceArgument);
        startInfo.ArgumentList.Add(instanceId);
        if (Process.Start(startInfo) == null)
        {
            throw new InvalidOperationException($"Failed to launch instance '{instanceId}'.");
        }
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();
}
