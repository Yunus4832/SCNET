using System.Globalization;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game;

public static class HeadlessEntry
{
    private static GameModRuntime? _modRuntime;

    private static volatile bool _running = true;

    public static void RequestStop()
    {
        _running = false;
    }

    public static int Main(RunningSetting runningSetting)
    {
        try
        {
            GameExitManager.BeginSession();
            RunMode.Value = RunModeType.HeadlessServer;
            _running = true;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Dispatcher.Initialize();
            Log.MinimumLogType = runningSetting.LogLevel;
            Log.AddLogSink(new ConsoleLogSink { MinimumLogType = runningSetting.LogLevel });
            Log.AddLogSink(new GameLogSink());

#if !ANDROID
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _running = false;
            };
#endif

            if (!InitializeHeadless())
            {
                Log.Information("Headless initialization requested restart. Exiting current process.");
                return 0;
            }

            var world = SessionInfoManager.ResolveHeadlessWorld(runningSetting);
            Log.Information($"Selected world: {world.WorldSettings.Name} ({world.DirectoryName})");
            Log.Information(
                $"Server ports: game={SettingsManager.ServerPort}, broadcast={SettingsManager.BroadcastPort}");
            CommonLib.WorkType = WorkType.Server;
            var gamesWidget = new GamesWidget();
            GameManager.LoadProject(world, gamesWidget);
            if (!CommonLib.StartServer())
            {
                Log.Error("Failed to start server (port may be in use).");
                return 3;
            }

            Log.Information("Headless server started. Press Ctrl+C to stop.");
            RunMainLoop();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return 1;
        }
        finally
        {
            try
            {
                CommonLib.Net.StopImmediate();
                GameManager.SaveProject(waitForCompletion: true, showErrorDialog: false);
                GameManager.DisposeProject();
                _modRuntime?.Dispose();
                _modRuntime = null;
                CurrentModRuntime.Set(null);
                SettingsManager.SaveSettings();
                Log.RemoveAllLogSinks();
                GameLogSink.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }
    }

    private static void RunMainLoop()
    {
        var sw = Stopwatch.StartNew();
        long nextTickMs = 0;
        const int tickMs = 50; // 20 TPS

        while (_running)
        {
            try
            {
                Time.BeforeFrame();
                Dispatcher.BeforeFrame();
                CommonLib.Net.Update();
                GameManager.UpdateProject();
                AsyncDispatcher.Update();
                Dispatcher.AfterFrame();
                Time.AfterFrame();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            nextTickMs += tickMs;
            var delay = nextTickMs - sw.ElapsedMilliseconds;
            switch (delay)
            {
                case > 0:
                    Thread.Sleep((int)delay);
                    break;
                case < -1000:
                    nextTickMs = sw.ElapsedMilliseconds;
                    break;
            }
        }
    }

    private static bool InitializeHeadless()
    {
        SettingsManager.Initialize();
        ContentManager.Initialize();
        PackageManager.Initialize();
        LocalModsImportManager.ImportInstalledMods(
            Storage.GetSystemPath(GamePaths.Mods),
            Storage.GetSystemPath(GamePaths.ModCache),
            Log.Information
        );

        var runningSetting = RunningSettingManager.Current;
        var startupSession = SessionInfoManager.ResolveStartupSession(runningSetting);
        if (StartupModProfileBootstrapper.EnsureStartupSessionProfile(
                runningSetting.ActiveSessionId,
                startupSession,
                Storage.GetSystemPath(GamePaths.ModCache),
                Log.Information))
        {
            return false;
        }

        var profile = ModProfileManager.LoadEffectiveProfile(runningSetting.ActiveSessionId, startupSession);
        _modRuntime = GameModRuntime.StartFromProfile(
            profile,
            Storage.GetSystemPath(GamePaths.ModCache),
            ModSide.Server,
            Log.Information
        );
        CurrentModRuntime.Set(_modRuntime);
        _modRuntime.InitializeLanguage(AppConfigStore.Values.TryGetValue("Language", out var language)
            ? language
            : "zh-CN"
        );
        _modRuntime.InitializeContentData();

        LightingManager.Initialize();
        CharacterSkinsManager.Initialize();
        VersionsManager.Initialize();
        WorldsManager.Initialize();
        return true;
    }
}
