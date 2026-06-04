using System.Globalization;
using System.Security.Cryptography;
using System.Xml.Linq;

using Game.Network;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game;

public static class HeadlessEntry
{
    private static volatile bool _running = true;

    public static void RequestStop()
    {
        _running = false;
    }

    public static int Main(RunningSetting runningSetting)
    {
        try
        {
            RunMode.Value = RunModeType.HeadlessServer;
            _running = true;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Dispatcher.Initialize();
#if DEBUG
            Log.AddLogSink(new ConsoleLogSink { MinimumLogType = LogType.Debug });
#else
            Log.AddLogSink(new ConsoleLogSink { MinimumLogType = LogType.Information });
#endif
            Log.AddLogSink(new GameLogSink());

#if !ANDROID
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                _running = false;
            };
#endif

            InitializeHeadless();
            var world = ResolveWorld(runningSetting);
            Log.Information($"Selected world: {world.WorldSettings.Name} ({world.DirectoryName})");
            Log.Information($"Server ports: game={SettingsManager.ServerPort}, broadcast={SettingsManager.BroadcastPort}");
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
                SettingsManager.SaveSettings();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
#if ANDROID
            Environment.Exit(0);
#endif
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

    private static WorldInfo ResolveWorld(RunningSetting runningSetting)
    {
        var worldArg = string.IsNullOrWhiteSpace(runningSetting.World) ? "World" : runningSetting.World;
        var seedArg = runningSetting.Seed;

        WorldsManager.UpdateWorldsList();
        var worlds = WorldsManager.WorldInfos.ToList();
        Log.Information($"Worlds directory: {ModsManager.WorldsDirectoryName}");
        Log.Information($"Detected worlds: {string.Join(", ", worlds.Select(w => w.WorldSettings.Name))}");

        var worldName = worlds.FirstOrDefault(w =>
            string.Equals(w.DirectoryName, worldArg, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(w.WorldSettings.Name, worldArg, StringComparison.OrdinalIgnoreCase));
        if (worldName == null)
        {
            var worldPath = Storage.CombinePaths(ModsManager.WorldsDirectoryName, worldArg);
            if (Storage.DirectoryExists(worldPath))
            {
                worldName = WorldsManager.GetWorldInfo(worldPath);
            }
        }
        if (worldName == null)
        {
            var worldSettings = new WorldSettings
            {
                Name = worldArg,
                Seed = string.IsNullOrWhiteSpace(seedArg) ? GenerateRandomSeed() : seedArg,
                OriginalSerializationVersion = VersionsManager.SerializationVersion,
                RunServer = true,
                IsNeedCommunityLogin = false
            };
            var customWorldDirectoryName = Storage.CombinePaths(ModsManager.WorldsDirectoryName, worldArg);
            Log.Information($"Creating new world with seed: {worldSettings.Seed}");
            worldName = WorldsManager.CreateWorld(worldSettings, customWorldDirectoryName);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(seedArg))
            {
                Log.Warning($"World already exists; ignoring provided seed \"{seedArg}\".");
            }
            Log.Information($"Using existing world seed: {worldName.WorldSettings.Seed}");
        }

        return worldName;
    }

    private static string GenerateRandomSeed()
    {
        var seed = RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);
        return seed.ToString(CultureInfo.InvariantCulture);
    }

    private static void InitializeHeadless()
    {
        SettingsManager.Initialize();
        ContentManager.Initialize();
        ModsManager.Initialize();
        PackageManager.Initialize();
        DatabaseManager.Initialize();

        ModsManager.ModList.Clear();
        foreach (var mod in ModsManager.ModListAll)
        {
            if (!mod.IsDependencyChecked)
            {
                mod.CheckDependencies(ModsManager.ModList);
            }
        }

        foreach (var mod in ModsManager.ModListAll)
        {
            mod.IsDependencyChecked = false;
        }

        var langList = ContentManager.List("Lang");
        LanguageControl.LanguageTypes.Clear();
        foreach (var contentInfo in langList)
        {
            var lang = Path.GetFileNameWithoutExtension(contentInfo.Filename);
            if (!LanguageControl.LanguageTypes.Contains(lang))
            {
                LanguageControl.LanguageTypes.Add(lang);
            }
        }

        var defaultLang = LanguageControl.LanguageTypes.Contains("zh-CN") ? "zh-CN" : "en-US";
        LanguageControl.Initialize(defaultLang);
        ModsManager.ModListAllDo(mod => mod.LoadLanguage());
        LanguageControl.RefreshCommonWords();

        ModsManager.ModListAllDo(mod => mod.LoadDll());
        ModsManager.ModListAllDo(mod => mod.LoadXdb(ref DatabaseManager.DatabaseNodeField));
        if (DatabaseManager.DatabaseNodeField is { } dbNode)
        {
            DatabaseManager.LoadDataBaseFromXml(dbNode);
        }
        else
        {
            throw new InvalidOperationException("Database node is null.");
        }

        BlocksManager.Initialize();
        LightingManager.Initialize();
        CraftingRecipesManager.Initialize();
        CharacterSkinsManager.Initialize();
        VersionsManager.Initialize();
        WorldsManager.Initialize();

        if (Storage.FileExists(ModsManager.ModsSettingPath))
        {
            using var stream = Storage.OpenFile(ModsManager.ModsSettingPath, OpenFileMode.Read);
            var element = XElement.Load(stream);
            ModsManager.LoadModSettings(element);
        }

        var modActions = new List<Action>();
        ModsManager.ModListAllDo(mod => mod.Loader?.OnLoadingStart(modActions));
        foreach (var action in modActions)
        {
            action();
        }

        ModsManager.ModListAllDo(mod => mod.Loader?.OnLoadingFinished([]));
    }
}
