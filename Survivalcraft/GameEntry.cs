using System.Globalization;
using System.Runtime.InteropServices;

using Engine.Graphics;
using Engine.Input;
using Engine.Network.Diagnostics;

using Game.Application;
using Game.Automation;
using Game.ClientContent;
using Game.Commands;
using Game.Commands.Http;
using Game.Diagnostics;
using Game.FileStorage;
using Game.Network;

namespace Game;

public static class GameEntry
{
    private static double _frameBeginTime;

    private static TimeSpan _processCpuTimeBegin;

    private static TimeSpan _processCpuTimeEnd;

    /// <summary>用户最近一次调整后的窗口逻辑尺寸（用于退出时保存到 RunningSetting）。</summary>
    private static Point2 _lastWindowSize;

    /// <summary>窗口初始化稳定后才开始记录用户调整，排除启动时的自动缩放事件。</summary>
    private static bool _windowStateReady;

    [STAThread]
    public static GameExitAction EntryPoint(StartupContext startup)
    {
        var runningSetting = startup.Settings;
        GameExitManager.BeginSession();
        NetworkLogging.Configure((level, message) =>
            {
                switch (level)
                {
                    case NetworkLogLevel.Debug:
                        Log.Debug(message);
                        break;
                    case NetworkLogLevel.Warning:
                        Log.Warning(message);
                        break;
                    case NetworkLogLevel.Error:
                        Log.Error(message);
                        break;
                    default:
                        Log.Information(message);
                        break;
                }
            }
        );
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        Log.MinimumLogType = runningSetting.LogLevel;
        Log.AddLogSink(new ConsoleLogSink { MinimumLogType = runningSetting.LogLevel });
        Log.AddLogSink(new GameLogSink());

        Window.HandleUri += HandleUriHandler;
        Window.Deactivated += DeactivatedHandler;
        Window.Frame += FrameHandler;
        Window.Resized += OnWindowResized;
        Window.Closing += PrepareClose;
        Window.Closed += Closed;
        Display.DeviceReset += ContentManager.DisplayDeviceReset;
        Window.UnhandledException += delegate(UnhandledExceptionInfo e)
        {
            DialogsManager.Alert(
                Localization.ClientLanguage.Resolver.Get(
                    "GameEntry",
                    "UnhandledException",
                    "Unhandled exception: {0}",
                    e.Exception.Message
                ),
                e.Exception.StackTrace ?? string.Empty
            );
            Log.Error(e.Exception.Message);
            e.IsHandled = true;
        };
        Window.Run(
            runningSetting.WindowWidth,
            runningSetting.WindowHeight,
            StartupWindowModes.ToWindowMode(runningSetting.WindowMode),
            VersionsManager.Title
        );
        return GameExitManager.ExitAction;
    }

    public static void HandleUriHandler(Uri uri)
    {
        ClientApplication.EnqueueUri(uri);
    }

    public static void DeactivatedHandler()
    {
        GC.Collect();
    }

    public static void FrameHandler()
    {
        if (Time.FrameIndex < 0)
        {
            Display.Clear(Vector4.Zero, 1f);
            return;
        }

        if (Time.FrameIndex == 0)
        {
            Initialize();
            return;
        }

        if (Time.FrameIndex == 30)
        {
            _windowStateReady = true;
        }

        Run();
    }

    private static void OnWindowResized()
    {
        if (!_windowStateReady)
        {
            return;
        }

        _lastWindowSize = new Point2(Window.View.Size.X, Window.View.Size.Y);
    }

    private static bool PrepareClose()
    {
        try
        {
            GameManager.SaveProject(waitForCompletion: true, showErrorDialog: true);
            return true;
        }
        catch (WorldSaveException) { return false; }
    }

    public static void Closed()
    {
        var errors = new List<Exception>();

        void Cleanup(Action action)
        {
            try { action(); }
            catch (Exception error) { errors.Add(error); }
        }

        Cleanup(AutomationInputController.Clear);
        Cleanup(HttpCommandHostManager.Stop);
        if (_windowStateReady && _lastWindowSize is { X: > 0, Y: > 0 })
        {
            Cleanup(() => StartupManager.Profiles.UpdateRunningSettings(rs =>
                    {
                        rs.WindowWidth = _lastWindowSize.X;
                        rs.WindowHeight = _lastWindowSize.Y;
                    }
                )
            );
        }

        if (CommonLib.IsNetworkInitialized)
        {
            Cleanup(GameManager.DisposeProject);
            Cleanup(CommonLib.Net.StopImmediate);
        }

        Cleanup(() => SetModRuntime(null));
        Cleanup(SettingsManager.SaveSettings);
        if (errors.Count > 0) { throw new AggregateException("Failed to release game application resources.", errors); }
    }

    public static void InitializeClientContent(GameModRuntime runtime)
    {
        var modules = new List<IClientContentModule> { new BuiltInClientContentModule() };
        foreach (var mod in runtime.Host.Runtimes)
        {
            if (mod.Descriptor.Manifest.ClientContentEntrypoint is not { } entrypoint) { continue; }

            var loader = mod.Descriptor.Entrypoints ??
                         throw new InvalidOperationException(
                             $"Mod {mod.Descriptor.Manifest.Id} has no entrypoint loader."
                         );
            modules.Add(loader.CreateEntrypoint<IClientContentModule>(entrypoint));
        }

        var replacement = new ClientContentRuntime(modules);
        try { replacement.InstallResources(runtime.Content.RegisteredResources); }
        catch
        {
            replacement.Dispose();
            throw;
        }

        ClientApplication.InstallContent(replacement);
    }

    public static void InitializeClientPreview(GameModRuntime runtime)
    {
        var content = ClientApplication.Content;
        content.InitializePreview(runtime.PreviewCatalog);
        var gradient = ContentManager.Get<Texture2D>("Textures/SeasonsSlider").Tag as Engine.Media.Images.Image ??
                       throw new InvalidOperationException("Season gradient image is not available.");
        content.Seasons.InitializeGradient(gradient);
    }

    public static void SetModRuntime(GameModRuntime? runtime)
    {
        using var replacement = CurrentModRuntime.BeginReplacement(runtime);
        try { ClientApplication.ReleaseContent(); }
        catch (Exception error) { Log.Error($"Client content retirement cleanup failed: {error}"); }

        replacement.Commit();
        if (runtime is not null)
        {
            HttpCommandHostManager.Start(
                StartupManager.Current.Session,
                CommandPrincipal.LocalOperator
            );
        }
    }

    public static void Initialize()
    {
        ClientApplication.ConfigurePaths(
            new ClientStoragePaths(
                GamePaths.External,
                GamePaths.ScreenCaptures,
                GamePaths.CharacterSkins,
                GamePaths.FurniturePacks,
                GamePaths.BlockTextures,
                GamePaths.ContentPackageCache,
                GamePaths.ContentPackageCreationTemp,
                GamePaths.Logs
            )
        );
        ClientApplication.InstallWorldRepository(
            new WorldRepository(
                GamePaths.Worlds,
                [],
                GameManager.WorldActivity,
                WorldProjectPersistence.WaitForPendingSaves
            )
        );
        Log.Information(
            $"Survivalcraft starting up at {DateTime.Now}, Version={VersionsManager.Version}, ProtocolVersion={VersionsManager.ProtocolVersion}, Platform={Platforms.ClientPlatform.Kind}, Storage.AvailableFreeSpace={Storage.FreeSpace / 1024 / 1024}MB, ApproximateScreenDpi={ScreenResolutionManager.ApproximateScreenDpi:0.0}, ApproxScreenInches={ScreenResolutionManager.ApproximateScreenInches:0.0}, ScreenResolution={Window.Size}, ProcessorsCount={Environment.ProcessorCount}, 64bit={Marshal.SizeOf<IntPtr>() == 8}"
        );
        ModProfileManager.Initialize(ClientApplication.Worlds);
        SettingsManager.Initialize();
        CommonLib.InitializeNetwork(
            new NetNodeOptions(
                Math.Max(1, Configuration.HostingConfiguration.Preferences.ServerChunkCountSendPer),
                Math.Max(1, Configuration.HostingConfiguration.Preferences.ServerChunkCountSendPer),
                Math.Max(1, Configuration.HostingConfiguration.Preferences.ServerChunkBytesSendPerSecond)
            ),
            true
        );
        LocalIdentityManager.Initialize(Storage.GetSystemPath(GamePaths.LocalIdentitiesFile));
        AuthenticationManager.Initialize();
        ContentRepositoryManager.Initialize(ExceptionManager.ReportExceptionToUser);
        ServerDirectoryManager.Initialize(ExceptionManager.ReportExceptionToUser);
        RendezvousManager.Initialize(ExceptionManager.ReportExceptionToUser);
        ContentManager.Initialize();
        ScreensManager.Initialize(new LoadingScreen());
    }

    public static void Run()
    {
        var realTime = Time.RealTime;
        var currentCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        ClientApplication.LastFrameTime = (float)(realTime - _frameBeginTime);
        ClientApplication.LastCpuFrameTime =
            (float)((_processCpuTimeEnd - _processCpuTimeBegin).TotalSeconds / Environment.ProcessorCount);
        _frameBeginTime = realTime;
        _processCpuTimeBegin = currentCpuTime;
        if (Keyboard.IsKeyDownOnce(Key.F11))
        {
            var windowMode = StartupManager.Profiles.CurrentRunningSettings.WindowMode == StartupWindowMode.Fullscreen
                ? WindowMode.Resizable
                : WindowMode.Fullscreen;
            Window.WindowMode = windowMode;
            StartupManager.Profiles.UpdateRunningSettings(rs =>
                rs.WindowMode = StartupWindowModes.FromWindowMode(windowMode)
            );
        }

        Window.VSync = Configuration.ClientConfiguration.Preferences.VSync;
        try
        {
            if (ExceptionManager.Error == null)
            {
                ClientApplication.DispatchUris();

                PerformanceManager.Update();
                MusicManager.Update();
                GameManager.UpdateRuntime();
                ScreensManager.Update();
                DialogsManager.Update();
                DebugOverlayManager.Update();
                AutomationInputController.Update();
                HttpCommandHostManager.Update();
            }
            else
            {
                ExceptionManager.UpdateExceptionScreen();
            }
        }
        catch (WorldSaveException)
        {
            // Saving already reported the failure. Keep the current world and screen available for retry.
        }
        catch (Exception e)
        {
            ExceptionManager.ReportExceptionToUser(string.Empty, e);
            ScreensManager.SwitchScreen("MainMenu");
        }

        try
        {
            Display.RenderTarget = null;
            if (ExceptionManager.Error == null)
            {
                ScreensManager.Draw();
                PerformanceManager.Draw();
                DebugOverlayManager.Draw();
                ScreenCaptureManager.Run();
            }
            else
            {
                ExceptionManager.DrawExceptionScreen();
            }

            _processCpuTimeEnd = Process.GetCurrentProcess().TotalProcessorTime;
        }
        catch (Exception e2)
        {
            ExceptionManager.ReportExceptionToUser(string.Empty, e2);
            ScreensManager.SwitchScreen("MainMenu");
        }
    }
}
