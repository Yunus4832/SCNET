using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Engine.Graphics;
using Engine.Input;

using Game.Network;

using LiteNetLib;

namespace Game;

public static class Program
{
    public const string Scheme = "com.candy.survivalcraft";

    private static double _frameBeginTime;

    private static TimeSpan _processCpuTimeBegin;

    private static TimeSpan _processCpuTimeEnd;

    private static readonly List<HandleUriItem> _urisToHandle = [];

    public static Action<string, string> RamDataChangeException = delegate { }; //内存数值被修改事件

    public static float LastFrameTime { get; set; }

    public static float LastCpuFrameTime { get; set; }

    public static event Action<HandleUriItem>? HandleUri;

#if DESKTOP
    public static void Main(string[]? args)
    {
        if (args != null)
        {
            foreach (var c in args)
            {
                if (c.StartsWith(Scheme))
                {
                    HandleUriHandler(new Uri(c));
                }
            }
        }

        EntryPoint();
    }
#endif

    [STAThread]
    public static void EntryPoint()
    {
        NetDebug.Logger = new LiteNetLog();
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

#if DEBUG
        Log.AddLogSink(new ConsoleLogSink { MinimumLogType = LogType.Debug });
#else
        Log.AddLogSink(new ConsoleLogSink { MinimumLogType = LogType.Information });
#endif
        Log.AddLogSink(new GameLogSink());

        Window.HandleUri += HandleUriHandler;
        Window.Deactivated += DeactivatedHandler;
        Window.Frame += FrameHandler;
        Window.Closed += Closed;
        RamDataChangeException += (_, _) =>
        {
            var modPath = Storage.GetSystemPath(ModsManager.ModsPath);
            if (Directory.Exists(modPath) && Directory.GetFiles(modPath).Length > 0)
            {
                return;
            }

            Log.Warning("no zuo no die");
            try
            {
                var player = GameManager.Project?.FindSubsystem<SubsystemPlayers>()?.MainPlayer;
                player?.ComponentHealth.Injure(1.0f, null, true, "尝试作弊被审判死神带走");
            }
            catch
            {
                // ignored
            }

            Environment.Exit(0);
        };

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
        ScreensManager.OnEnterScreen += AutoEnterServer.EnterServer;

        Window.Run(0, 0, WindowMode.Resizable, VersionsManager.Title);
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

        Run();
    }

    public static void Closed()
    {
        SettingsManager.SaveSettings();
    }

    public static void Initialize()
    {
        Log.Information(
            $"Survivalcraft starting up at {DateTime.Now}, Version={VersionsManager.Version}, BuildConfiguration={VersionsManager.BuildConfiguration}, Platform={VersionsManager.Platform}, Storage.AvailableFreeSpace={Storage.FreeSpace / 1024 / 1024}MB, ApproximateScreenDpi={ScreenResolutionManager.ApproximateScreenDpi:0.0}, ApproxScreenInches={ScreenResolutionManager.ApproximateScreenInches:0.0}, ScreenResolution={Window.Size}, ProcessorsCount={Environment.ProcessorCount}, RAM={Utilities.GetTotalAvailableMemory() / 1024 / 1024}MB, 64bit={Marshal.SizeOf<IntPtr>() == 8}");
        SettingsManager.Initialize();
        AnalyticsManager.Initialize();
        VersionsManager.Initialize();
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
            SettingsManager.WindowMode = SettingsManager.WindowMode == WindowMode.Fullscreen
                ? WindowMode.Resizable
                : WindowMode.Fullscreen;
        }

        Window.VSync = SettingsManager.VSync;
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
            var s = level.ToString();
            if (!SettingsManager.LiteNetLibLogLevel.Split([','], StringSplitOptions.None).Contains(s))
            {
                return;
            }

            var builder = new StringBuilder();
            builder.Append("[LiteNetLib]");
            builder.Append(s);
            builder.Append(str);
            foreach (var obj in args)
            {
                builder.Append(obj);
                builder.Append(' ');
            }

            Log.Information(builder.ToString());
        }
    }
}
