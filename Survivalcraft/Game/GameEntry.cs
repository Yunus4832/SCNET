using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using Engine.Graphics;
using Engine.Input;

using Game.Network;

using LiteNetLib;

namespace Game;

public static class GameEntry
{
    public const string Scheme = "com.candy.survivalcraft";

    private static double _frameBeginTime;

    private static TimeSpan _processCpuTimeBegin;

    private static TimeSpan _processCpuTimeEnd;

    private static readonly List<HandleUriItem> _urisToHandle = [];

    public static GameModRuntime? ModRuntime => CurrentModRuntime.Value;

    public static Action<string, string> RamDataChangeException = delegate { }; //内存数值被修改事件

    public static float LastFrameTime { get; set; }

    public static float LastCpuFrameTime { get; set; }

    public static event Action<HandleUriItem>? HandleUri;

#if DESKTOP
    public static GameExitAction Main(RunningSetting runningSetting)
    {
        foreach (var arg in runningSetting.RemainingArgs)
        {
            if (arg.StartsWith(Scheme))
            {
                HandleUriHandler(new Uri(arg));
            }
        }

        return EntryPoint(runningSetting);
    }
#endif

    [STAThread]
    public static GameExitAction EntryPoint(RunningSetting runningSetting)
    {
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
        Window.Closed += Closed;
        RamDataChangeException += (_, _) =>
        {
            var modPath = Storage.GetSystemPath(GamePaths.Mods);
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

            Window.Close();
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
        Window.Run(0, 0, WindowMode.Resizable, VersionsManager.Title);
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

        Run();
    }

    public static void Closed()
    {
        if (GameExitManager.ExitAction is not GameExitAction.Restart)
        {
            SessionInfoManager.Save(SessionInfoManager.CaptureCurrentSession());
            RunningSettingManager.SetRestore(false);
        }

        ModRuntime?.Dispose();
        CurrentModRuntime.Set(null);
        SettingsManager.SaveSettings();
    }

    public static void SetModRuntime(GameModRuntime? runtime)
    {
        ModRuntime?.Dispose();
        CurrentModRuntime.Set(runtime);
    }

    public static void Initialize()
    {
        Log.Information(
            $"Survivalcraft starting up at {DateTime.Now}, Version={VersionsManager.Version}, ProtocolVersion={VersionsManager.ProtocolVersion}, BuildConfiguration={VersionsManager.BuildConfiguration}, Platform={VersionsManager.Platform}, Storage.AvailableFreeSpace={Storage.FreeSpace / 1024 / 1024}MB, ApproximateScreenDpi={ScreenResolutionManager.ApproximateScreenDpi:0.0}, ApproxScreenInches={ScreenResolutionManager.ApproximateScreenInches:0.0}, ScreenResolution={Window.Size}, ProcessorsCount={Environment.ProcessorCount}, RAM={Utilities.GetTotalAvailableMemory() / 1024 / 1024}MB, 64bit={Marshal.SizeOf<IntPtr>() == 8}");
        SettingsManager.Initialize();
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
