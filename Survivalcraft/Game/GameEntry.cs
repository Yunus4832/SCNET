using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Engine.Graphics;
using Engine.Input;

using Game.Commands;
using Game.Network;
using Game.Automation;

using LiteNetLib;

namespace Game;

public static class GameEntry
{
    private static double _frameBeginTime;

    private static TimeSpan _processCpuTimeBegin;

    private static TimeSpan _processCpuTimeEnd;

    private static readonly List<HandleUriItem> _urisToHandle = [];

    /// <summary>用户最近一次调整后的窗口逻辑尺寸（用于退出时保存到 RunningSetting）。</summary>
    private static Point2 _lastWindowSize;

    /// <summary>窗口初始化稳定后才开始记录用户调整，排除启动时的自动缩放事件。</summary>
    private static bool _windowStateReady;

    public static GameModRuntime? ModRuntime => CurrentModRuntime.Value;

    public static float LastFrameTime { get; set; }

    public static float LastCpuFrameTime { get; set; }

    public static event Action<HandleUriItem>? HandleUri;

    [STAThread]
    public static GameExitAction EntryPoint(StartupContext startup)
    {
        var runningSetting = startup.Settings;
        GameExitManager.BeginSession();
        NetDebug.Logger = new LiteNetLog();
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        Log.MinimumLogType = runningSetting.LogLevel;
        Log.AddLogSink(new ConsoleLogSink { MinimumLogType = runningSetting.LogLevel });
        Log.AddLogSink(new GameLogSink());

        Window.HandleUri += HandleUriHandler;
        Window.Deactivated += DeactivatedHandler;
        Window.Frame += FrameHandler;
        Window.Resized += OnWindowResized;
        Window.Closed += Closed;
        Display.DeviceReset += ContentManager.DisplayDeviceReset;
        Window.UnhandledException += delegate(UnhandledExceptionInfo e)
        {
            DialogsManager.Alert(
                "未处理的异常" + e.Exception.Message,
                e.Exception.StackTrace ?? string.Empty
            );
            Log.Error(e.Exception.Message);
            e.IsHandled = true;
        };
        Window.Run(
            runningSetting.WindowWidth,
            runningSetting.WindowHeight,
            runningSetting.WindowMode,
            VersionsManager.Title);
        return GameExitManager.ExitAction;
    }

    public static void HandleUriHandler(Uri uri)
    {
        _urisToHandle.Add(new HandleUriItem(uri));
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

    public static void Closed()
    {
        AutomationInputController.Clear();
        HttpCommandHostManager.Stop();
        if (_windowStateReady && _lastWindowSize is { X: > 0, Y: > 0 })
        {
            RunningSettingManager.SaveCurrent(rs =>
            {
                rs.WindowWidth = _lastWindowSize.X;
                rs.WindowHeight = _lastWindowSize.Y;
            });
        }

        ModRuntime?.Dispose();
        CurrentModRuntime.Set(null);
        SettingsManager.SaveSettings();
    }

    public static void SetModRuntime(GameModRuntime? runtime)
    {
        ModRuntime?.Dispose();
        CurrentModRuntime.Set(runtime);
        if (runtime is not null)
        {
            HttpCommandHostManager.Start(
                StartupManager.Current.Session,
                CommandPrincipal.ApplicationUser);
        }
    }

    public static void Initialize()
    {
        Log.Information(
            $"Survivalcraft starting up at {DateTime.Now}, Version={VersionsManager.Version}, ProtocolVersion={VersionsManager.ProtocolVersion}, BuildConfiguration={VersionsManager.BuildConfiguration}, Platform={PlatformManager.Platform}, Storage.AvailableFreeSpace={Storage.FreeSpace / 1024 / 1024}MB, ApproximateScreenDpi={ScreenResolutionManager.ApproximateScreenDpi:0.0}, ApproxScreenInches={ScreenResolutionManager.ApproximateScreenInches:0.0}, ScreenResolution={Window.Size}, ProcessorsCount={Environment.ProcessorCount}, RAM={Utilities.GetTotalAvailableMemory() / 1024 / 1024}MB, 64bit={Marshal.SizeOf<IntPtr>() == 8}");
        SettingsManager.Initialize();
        ExternalContentManager.Initialize();
        ContentManager.Initialize();
        ScreensManager.Initialize();
    }

    public static void Run()
    {
        var realTime = Time.RealTime;
        var currentCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        LastFrameTime = (float)(realTime - _frameBeginTime);
        LastCpuFrameTime =
            (float)((_processCpuTimeEnd - _processCpuTimeBegin).TotalSeconds / Environment.ProcessorCount);
        _frameBeginTime = realTime;
        _processCpuTimeBegin = currentCpuTime;
        if (Keyboard.IsKeyDownOnce(Key.F11))
        {
            var windowMode = RunningSettingManager.Current.WindowMode == WindowMode.Fullscreen
                ? WindowMode.Resizable
                : WindowMode.Fullscreen;
            Window.WindowMode = windowMode;
            RunningSettingManager.SaveCurrent(rs => rs.WindowMode = windowMode);
        }

        Window.VSync = SettingsManager.Current.VSync;
        try
        {
            if (ExceptionManager.Error == null)
            {
                if (_urisToHandle.Count > 0)
                {
                    var list = new List<HandleUriItem>();
                    foreach (var obj in _urisToHandle)
                    {
                        HandleUri?.Invoke(obj);
                        if (obj.IsHandle)
                        {
                            list.Add(obj);
                        }
                    }

                    foreach (var obj in list)
                    {
                        _urisToHandle.Remove(obj);
                    }
                }

                PerformanceManager.Update();
                MotdManager.Update();
                MusicManager.Update();
                ScreensManager.Update();
                DialogsManager.Update();
                AutomationInputController.Update();
                HttpCommandExecutionQueue.Update();
                AsyncDispatcher.Update();
            }
            else
            {
                ExceptionManager.UpdateExceptionScreen();
            }
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

    public class HandleUriItem(Uri uri)
    {
        public bool IsHandle = false;

        public readonly Uri Uri = uri;
    }

    private class LiteNetLog : INetLogger
    {
        public void WriteNet(NetLogLevel level, string str, params object[] args)
        {
            var logType = MapLogType(level);
            var builder = new StringBuilder();
            builder.Append("[LiteNetLib]");
            builder.Append(level);
            builder.Append(' ');
            builder.Append(FormatMessage(str, args));
            Write(logType, builder.ToString());
        }

        private static string FormatMessage(string message, object[] args)
        {
            if (args.Length == 0)
            {
                return message;
            }

            try
            {
                return string.Format(message, args);
            }
            catch
            {
                var builder = new StringBuilder(message);
                foreach (var arg in args)
                {
                    builder.Append(' ');
                    builder.Append(arg);
                }

                return builder.ToString();
            }
        }

        private static LogType MapLogType(NetLogLevel level)
        {
            return level switch
            {
                NetLogLevel.Trace => LogType.Debug,
                NetLogLevel.Info => LogType.Information,
                NetLogLevel.Warning => LogType.Warning,
                NetLogLevel.Error => LogType.Error,
                _ => LogType.Information
            };
        }

        private static void Write(LogType logType, string message)
        {
            switch (logType)
            {
                case LogType.Debug:
                    Log.Debug(message);
                    break;
                case LogType.Verbose:
                    Log.Verbose(message);
                    break;
                case LogType.Information:
                    Log.Information(message);
                    break;
                case LogType.Warning:
                    Log.Warning(message);
                    break;
                case LogType.Error:
                    Log.Error(message);
                    break;
                default:
                    Log.Information(message);
                    break;
            }
        }
    }
}
