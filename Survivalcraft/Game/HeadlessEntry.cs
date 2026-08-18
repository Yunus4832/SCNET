using System.Collections.Concurrent;
using System.Globalization;

using Game.Commands;
using Game.Network;
using Game.Network.Enums;
using Game.Network.Serialization;

namespace Game;

public sealed record HeadlessCommandSuggestions(
    IReadOnlyList<CommandSuggestion> Items,
    bool CanExecute);

public static class HeadlessEntry
{
    private sealed record ConsoleCommandRequest(
        string Input,
        bool WriteResultToLog,
        TaskCompletionSource<CommandResult>? Completion);

    private sealed record ConsoleSuggestionRequest(
        string Input,
        TaskCompletionSource<HeadlessCommandSuggestions> Completion);

    private static GameModRuntime? _modRuntime;

    private static volatile bool _running = true;

    private static volatile bool _commandConsoleReady;

    private static readonly ConcurrentQueue<ConsoleCommandRequest> _consoleCommands = new();

    private static readonly ConcurrentQueue<ConsoleSuggestionRequest> _consoleSuggestions = new();

    public static bool IsCommandConsoleReady => _commandConsoleReady;

    public static void RequestStop()
    {
        _running = false;
    }

    /// <summary>
    /// Enqueues a trusted host-console command for execution on the Headless
    /// server thread. The command uses the ServerOperator principal.
    /// </summary>
    public static Task<CommandResult> SubmitConsoleCommandAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.FromResult(CommandResult.LocalizedFail(
                "command.empty",
                "CommandEmpty_Message",
                "请输入指令。"));
        }

        if (!_commandConsoleReady)
        {
            return Task.FromResult(CreateCommandConsoleUnavailableResult());
        }

        var completion = new TaskCompletionSource<CommandResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _consoleCommands.Enqueue(new ConsoleCommandRequest(
            input.Trim(),
            WriteResultToLog: false,
            completion));
        return completion.Task;
    }

    /// <summary>
    /// Enqueues a trusted host-console completion request for evaluation on
    /// the Headless server thread.
    /// </summary>
    public static Task<HeadlessCommandSuggestions> SubmitConsoleSuggestionsAsync(string input)
    {
        if (!_commandConsoleReady)
        {
            return Task.FromResult(EmptyConsoleSuggestions());
        }

        var completion = new TaskCompletionSource<HeadlessCommandSuggestions>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _consoleSuggestions.Enqueue(new ConsoleSuggestionRequest(input ?? string.Empty, completion));
        return completion.Task;
    }

    public static int Main(StartupContext startup)
    {
        var runningSetting = startup.Settings;
        try
        {
            GameExitManager.BeginSession();
            RunMode.Value = RunModeType.HeadlessServer;
            _running = true;
            SetCommandConsoleReady(false);
            FailPendingConsoleRequests();
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            Dispatcher.Initialize();
            Log.MinimumLogType = runningSetting.LogLevel;
            Log.AddLogSink(new ConsoleLogSink { MinimumLogType = runningSetting.LogLevel });
            Log.AddLogSink(new GameLogSink());

            if (PlatformManager.Platform is Platform.Desktop)
            {
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    _running = false;
                };
            }

            if (!InitializeHeadless())
            {
                Log.Information("Headless initialization requested restart. Exiting current process.");
                return 0;
            }

            var startupSession = startup.Session;
            var world = SessionInfoManager.ResolveHeadlessWorld(startup);
            var serverPort = startupSession.ServerPort > 0
                ? startupSession.ServerPort
                : SettingsManager.Current.ServerPort;
            var broadcastPort = startupSession.BroadcastPort > 0
                ? startupSession.BroadcastPort
                : SettingsManager.Current.BroadcastPort;
            Log.Information($"Selected world: {world.WorldSettings.Name} ({world.DirectoryName})");
            Log.Information(
                $"Server ports: game={serverPort}, broadcast={broadcastPort}");
            CommonLib.WorkType = WorkType.Server;
            var gamesWidget = new GamesWidget();
            GameManager.LoadProject(world, gamesWidget);
            if (!CommonLib.StartServer(startupSession))
            {
                Log.Error("Failed to start server (port may be in use).");
                return 3;
            }

            SetCommandConsoleReady(true);
            Log.Information("Headless server started. Press Ctrl+C to stop.");
            WriteAdministrationBootstrapInstructions();
            StartConsoleReader();
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
            SetCommandConsoleReady(false);
            FailPendingConsoleRequests();
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
                ExecuteConsoleCommands();
                ExecuteConsoleSuggestions();
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

    private static void StartConsoleReader()
    {
        if (PlatformManager.Platform is not Platform.Desktop)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            while (_running)
            {
                string? line;
                try
                {
                    line = Console.ReadLine();
                }
                catch (Exception exception)
                {
                    Log.Error($"Failed to read server console input: {exception}");
                    return;
                }

                if (line is null)
                {
                    return;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    _consoleCommands.Enqueue(new ConsoleCommandRequest(
                        line,
                        WriteResultToLog: true,
                        Completion: null));
                }
            }
        });
    }

    private static void ExecuteConsoleCommands()
    {
        while (_consoleCommands.TryDequeue(out var request))
        {
            var result = CommandExecutor.ExecuteServerOperator(
                request.Input,
                GameManager.Project);
            if (GameManager.Project is { } project)
            {
                CommandResultPublisher.Publish(project, result, includeServer: false);
            }

            if (request.WriteResultToLog)
            {
                WriteConsoleCommandResult(result);
            }

            request.Completion?.TrySetResult(result);
        }
    }

    private static void WriteConsoleCommandResult(CommandResult result)
    {
        var level = result.Success ? "OK" : "ERROR";
        var output = $"COMMAND {level} [{result.Code}] {CommandText.Resolve(result)}";
        if (result.Sensitive)
        {
            Console.WriteLine(output);
        }
        else
        {
            Log.Information(output);
        }
    }

    private static void ExecuteConsoleSuggestions()
    {
        while (_consoleSuggestions.TryDequeue(out var request))
        {
            HeadlessCommandSuggestions result;
            try
            {
                if (CurrentModRuntime.Value is not { } runtime)
                {
                    result = EmptyConsoleSuggestions();
                }
                else
                {
                    var adapter = new TextCommandAdapter(runtime.Commands);
                    var items = adapter.Suggest(
                            request.Input,
                            CommandPrincipal.ServerOperator,
                            CommandInvocationChannel.ServerControl)
                        .OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    result = new HeadlessCommandSuggestions(
                        items,
                        adapter.CanExecute(
                            request.Input,
                            CommandPrincipal.ServerOperator,
                            CommandInvocationChannel.ServerControl));
                }
            }
            catch (Exception exception)
            {
                Log.Error($"Failed to generate server console suggestions: {exception}");
                result = EmptyConsoleSuggestions();
            }

            request.Completion.TrySetResult(result);
        }
    }

    private static void SetCommandConsoleReady(bool ready)
    {
        _commandConsoleReady = ready;
    }

    private static void FailPendingConsoleRequests()
    {
        while (_consoleCommands.TryDequeue(out var request))
        {
            request.Completion?.TrySetResult(CreateCommandConsoleUnavailableResult());
        }

        while (_consoleSuggestions.TryDequeue(out var request))
        {
            request.Completion.TrySetResult(EmptyConsoleSuggestions());
        }
    }

    private static HeadlessCommandSuggestions EmptyConsoleSuggestions()
    {
        return new HeadlessCommandSuggestions([], false);
    }

    private static CommandResult CreateCommandConsoleUnavailableResult()
    {
        return CommandResult.LocalizedFail(
            "command.unavailable",
            "CommandUnavailable_Message",
            "指令系统尚未就绪。");
    }

    private static void WriteAdministrationBootstrapInstructions()
    {
        if (GameManager.Project is null ||
            !ServerAdministrationBootstrap.TryGetClaimCode(GameManager.Project, out var code))
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(CommandText.Get(
            "AuthConsoleBootstrap_Message",
            "服务器管理尚未初始化\n当前没有玩家拥有管理权限。\n认领码：{0}\n初始化管理权限：\n1. 以玩家身份连接此服务器。\n2. 执行：/auth claim {0}\n如有需要，可在此控制台执行 auth code 或 auth regenerate。",
            code));
        Console.WriteLine();
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

        var startupSession = StartupManager.Current.Session;
        var profile = ModProfileManager.LoadEffectiveProfile(
            startupSession.SessionId,
            startupSession);
        ModProfileResolver.EnsurePackagesAvailable(
            profile,
            Storage.GetSystemPath(GamePaths.ModCache),
            Log.Information
        );
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
        WorldsManager.Initialize();
        return true;
    }
}
