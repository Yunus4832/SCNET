using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

using Engine.Core.FileStorage;
using Engine.Core.Logging;
using Engine.Core.Runtime;

using Game.Commands;
using Game.Commands.BuiltIn;
using Game.Commands.Http;
using Game.Commands.Text;
using Game.Configuration;
using Game.Content;
using Game.Diagnostics;
using Game.FileStorage;
using Game.Hosting;
using Game.Localization;
using Game.Managers;
using Game.Modding;
using Game.Network;
using Game.Network.Handling;
using Game.Network.Snapshots;
using Game.Protocol;
using Game.Protocol.Commands;
using Game.Protocol.Localization;
using Game.Startup;
using Game.World;

namespace Survivalcraft.Server;

public sealed record ServerCommandSuggestions(
    IReadOnlyList<CommandSuggestion> Items,
    bool CanExecute
);

public static class ServerStarter
{
    public static int Main(string[] args)
    {
        try
        {
            var directory = AppContext.BaseDirectory;
            Storage.RegisterFileSystemRoot("app", directory, readOnly: true);
            Storage.RegisterFileSystemRoot("starter", directory);
            var instance = StarterInstanceManager.Initialize(args);
            var instancePath = Storage.GetSystemPath(instance.InstancePath);
            Storage.RegisterFileSystemRoot("external", instancePath);
            Storage.RegisterFileSystemRoot("data", Path.Combine(instancePath, "Data"));
            Storage.RegisterFileSystemRoot("config", Path.Combine(instancePath, "Config"));
            var startup = StartupManager.Load(instance.GameArguments, SessionTarget.World);
            var result = RunSession(startup);
            if (result == 0 && ExitAction is GameExitAction.Restart or GameExitAction.SwitchInstance)
            {
                ServerProcessLauncher.Restart(
                    ExitAction == GameExitAction.SwitchInstance ? SwitchInstanceId : instance.Id
                );
            }

            return result;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    public static ServerTickStatistics TickStatistics { get; } = new();

    private sealed record ConsoleCommandRequest(
        string Input,
        bool WriteResultToLog,
        TaskCompletionSource<CommandResult>? Completion
    );

    private sealed record ConsoleSuggestionRequest(
        string Input,
        TaskCompletionSource<ServerCommandSuggestions> Completion
    );

    private static readonly Lock _runLock = new();

    private static GameModRuntime? _modRuntime;

    private static readonly WorldProjectActivity _activity = new();

    private static readonly GameWorldController _worlds = new(_activity);

    private static NetNode? _net;

    private static WorldRepository _repository = null!;

    public static LanguageCatalog LanguageCatalog { get; private set; } = new();

    private static LocalizedTextResolver _resolver = new(LanguageCatalog);

    public static LocalizedTextResolver TextResolver => Volatile.Read(ref _resolver);

    private static PreferenceStore _preferences = null!;

    private static HttpCommandHost? _http;

    private static readonly HttpCommandExecutionQueue _httpQueue = new(
        () => _modRuntime is { } mods
            ? new CommandExecutionScope(
                mods.Commands,
                null,
                _worlds.Runtime?.Authority?.Project
            )
            : null,
        () => TextResolver
    );

    public static GameExitAction ExitAction { get; private set; }

    public static string SwitchInstanceId { get; private set; } = string.Empty;

    private static Action<IModCommands, ModId>? _registerAppCommands;

    public static NetworkCommandDiagnostics CommandDiagnostics { get; } = new(() => _net!, () => TickStatistics);

    public static ProcessCommandServices ProcessCommands { get; } = new(
        RequestRestart,
        RequestExit,
        RequestInstanceSwitch,
        () => LanguageCatalog,
        SetLanguage,
        SaveSettings
    );

    public static void SetLanguage(string language)
    {
        var mods = _modRuntime ?? throw new InvalidOperationException("Mod content is not ready.");
        LanguageCatalog = mods.Content.CreateLanguageCatalog(language);
        HostingConfiguration.Preferences.ConsoleLanguageType = LanguageCatalog.CurrentLanguage;
        Volatile.Write(ref _resolver, new LocalizedTextResolver(LanguageCatalog));
    }

    public static void SaveSettings() => _preferences.Save();

    public static void SaveWorldBeforeExit()
    {
        _worlds.Runtime?.Authority?.Project?.FindSubsystem<SubsystemWorldLifecycle>(true)?.Persistence.Save(true);
    }

    public static void RequestRestart(SessionInfo? session = null)
    {
        SaveWorldBeforeExit();
        var pending = StartupSessionLifecycle.PrepareRestart(
            session ?? _worlds.WorldSession ?? StartupManager.Current.Session,
            StartupManager.Profiles
        );
        StartupManager.Profiles.UpdateRunningSettings(settings => settings.PendingSessionId = pending.SessionId);
        ExitAction = GameExitAction.Restart;
        RequestStop();
    }

    public static void RequestInstanceSwitch(string instanceId)
    {
        SaveWorldBeforeExit();
        StarterInstanceManager.RequestSwitch(instanceId);
        SwitchInstanceId = instanceId;
        ExitAction = GameExitAction.SwitchInstance;
        RequestStop();
    }

    public static void RequestExit()
    {
        SaveWorldBeforeExit();
        ExitAction = GameExitAction.Exit;
        RequestStop();
    }

    private static bool _interactiveConsole;

    private static double _lastAutosave;

    private static volatile bool _running = true;

    private const int _stopRequestsClosed = -1;

    private static int _stopRequested;

    private static volatile bool _commandConsoleReady;

    private static readonly ConcurrentQueue<ConsoleCommandRequest> _consoleCommands = new();

    private static readonly ConcurrentQueue<ConsoleSuggestionRequest> _consoleSuggestions = new();

    public static bool IsCommandConsoleReady => _commandConsoleReady;

    public static void RequestStop()
    {
        Interlocked.CompareExchange(ref _stopRequested, 1, 0);
    }

    internal static CancellationTokenRegistration BeginStopRequestHandling(
        CancellationToken cancellationToken = default
    )
    {
        _running = true;
        // Preserve a request submitted before initialization starts.
        Interlocked.CompareExchange(ref _stopRequested, 0, _stopRequestsClosed);
        return cancellationToken.Register(RequestStop);
    }

    internal static bool ProcessStopRequest()
    {
        if (Interlocked.CompareExchange(ref _stopRequested, 0, 1) != 1) { return false; }

        // Only the server thread consumes requests. A failed save needs another explicit request.
        if (_worlds.Runtime?.Authority?.Project is { IsDisposed: false } project)
        {
            project.FindSubsystem<SubsystemWorldLifecycle>(true)!.RequestStop();
            return false;
        }

        EndStopRequestHandling();
        return true;
    }

    internal static void EndStopRequestHandling()
    {
        Interlocked.Exchange(ref _stopRequested, _stopRequestsClosed);
        _running = false;
    }

    /// <summary>
    ///     Enqueues a trusted host-console command for execution on the server
    ///     server thread. The command uses the LocalOperator principal.
    /// </summary>
    public static Task<CommandResult> SubmitConsoleCommandAsync(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Task.FromResult(
                CommandResult.LocalizedFail(
                    "command.empty",
                    "CommandEmpty_Message",
                    "请输入指令。"
                )
            );
        }

        if (!_commandConsoleReady)
        {
            return Task.FromResult(CreateCommandConsoleUnavailableResult());
        }

        var completion = new TaskCompletionSource<CommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _consoleCommands.Enqueue(
            new ConsoleCommandRequest(
                input.Trim(),
                WriteResultToLog: false,
                completion
            )
        );
        return completion.Task;
    }

    /// <summary>
    ///     Enqueues a trusted host-console completion request for evaluation on
    ///     the server server thread.
    /// </summary>
    public static Task<ServerCommandSuggestions> SubmitConsoleSuggestionsAsync(string input)
    {
        if (!_commandConsoleReady)
        {
            return Task.FromResult(EmptyConsoleSuggestions());
        }

        var completion = new TaskCompletionSource<ServerCommandSuggestions>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _consoleSuggestions.Enqueue(new ConsoleSuggestionRequest(input ?? string.Empty, completion));
        return completion.Task;
    }

    private static int RunSession(
        StartupContext startup,
        CancellationToken cancellationToken = default,
        bool interactiveConsole = true,
        Action<IModCommands, ModId>? registerAppCommands = null
    )
    {
        lock (_runLock)
        {
            _interactiveConsole = interactiveConsole;
            _registerAppCommands = registerAppCommands;
            ExitAction = GameExitAction.Exit;
            SwitchInstanceId = string.Empty;
            var stopRegistration = BeginStopRequestHandling(cancellationToken);
            var exitCode = 1;
            try { exitCode = RunServer(startup); }
            catch (Exception exception) { Log.Error(exception); }
            finally
            {
                stopRegistration.Dispose();
                EndStopRequestHandling();
                if (_interactiveConsole) { Console.CancelKeyPress -= HandleCancelKeyPress; }

                Interlocked.Exchange(ref _http, null)?.Dispose();
                _httpQueue.FailPending();
                SetCommandConsoleReady(false);
                FailPendingConsoleRequests();
                try
                {
                    if (_worlds.Runtime?.Authority?.Project is { IsDisposed: false } project)
                    {
                        project.FindSubsystem<SubsystemWorldLifecycle>(true)!.Persistence.Save(true);
                    }

                    _worlds.Close();
                    _net?.StopImmediate();
                    _net = null;

                    using (var replacement = CurrentModRuntime.BeginReplacement(null)) { replacement.Commit(); }

                    _modRuntime = null;
                    _preferences?.Save();
                    Log.RemoveAllLogSinks();
                    GameLogSink.Shutdown();
                }
                catch (Exception exception)
                {
                    Log.Error(exception);
                    exitCode = 1;
                }
            }

            return exitCode;
        }
    }

    private static void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = true;
        RequestStop();
    }

    private static int RunServer(StartupContext startup)
    {
        var runningSetting = startup.Settings;
        SetCommandConsoleReady(false);
        FailPendingConsoleRequests();
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        Dispatcher.Initialize();
        Log.MinimumLogType = runningSetting.LogLevel;
        Log.AddLogSink(new ConsoleLogSink { MinimumLogType = runningSetting.LogLevel });
        Log.AddLogSink(new GameLogSink());

        if (_interactiveConsole)
        {
            Console.CancelKeyPress += HandleCancelKeyPress;
        }

        if (!InitializeServices(startup))
        {
            Log.Information("Server initialization requested restart. Exiting current process.");
            return 0;
        }

        if (ProcessStopRequest()) { return 0; }

        var startupSession = startup.Session;
        var world = ServerWorldSelection.Resolve(startup, _repository, DatabaseManager.GameDatabase);
        var serverPort = startupSession.ServerPort > 0
            ? startupSession.ServerPort
            : HostingConfiguration.Preferences.ServerPort;
        var broadcastPort = startupSession.BroadcastPort switch
        {
            < 0 => "disabled",
            > 0 => startupSession.BroadcastPort.ToString(),
            _ => HostingConfiguration.Preferences.BroadcastPort.ToString()
        };
        Log.Information($"Selected world: {world.WorldSettings.Name} ({world.DirectoryName})");
        Log.Information($"Server ports: game={serverPort}, broadcast={broadcastPort}");
        var net = _net!;
        net.PrepareAuthorityWorld();
        WorldNetworkBinding.For(net.Runtime).InstallHandlers(
            HostRuleHandlers.CreateAuthority(
                _modRuntime!.Commands,
                _modRuntime.Network,
                _modRuntime.ModDataHash,
                new CharacterSkinStore(GamePaths.CharacterSkins),
                project => ServerInfoPackageBuilder.Capture(
                    project,
                    _modRuntime.CreateServerRequiredProfile(),
                    ContentRepositoryManager.Current.Snapshot()
                )
            )
        );
        _worlds.LoadAuthority(
            new WorldLoadRequest(
                world,
                startupSession,
                net,
                _modRuntime,
                DatabaseManager.GameDatabase,
                WorldProjectPolicies.CaptureAuthority(HostingConfiguration.Preferences),
                new BlockTextureStore(GamePaths.BlockTextures),
                false
            )
        );
        _worlds.Runtime!.Authority!.Project!.FindSubsystem<SubsystemWorldLifecycle>(true)!.Stopped +=
            _ => EndStopRequestHandling();
        if (!StartListener(startupSession, world))
        {
            Log.Error("Failed to start server (port may be in use).");
            return 3;
        }

        SetCommandConsoleReady(true);
        StartHttp(startup.Session);
        Log.Information("Server started. Press Ctrl+C to stop.");
        WriteAdministrationBootstrapInstructions();
        StartConsoleReader();
        RunMainLoop();
        return 0;
    }

    private static void RunMainLoop()
    {
        var sw = Stopwatch.StartNew();
        long nextTickMs = 0;
        const int tickMs = 50; // 20 TPS

        while (_running)
        {
            var tickStarted = Stopwatch.GetTimestamp();
            var startLateness = Math.Max(0, sw.Elapsed.TotalMilliseconds - nextTickMs);
            try
            {
                if (ProcessStopRequest()) { break; }

                Time.BeforeFrame();
                Dispatcher.BeforeFrame();
                if (ProcessStopRequest()) { break; }

                _worlds.Runtime?.UpdateMessages();
                _worlds.Runtime?.UpdateWorlds();
                if (_worlds.Runtime?.Authority?.Project is { IsDisposed: false } project &&
                    Time.RealTime - _lastAutosave > 120)
                {
                    _lastAutosave = Time.RealTime;
                    project.FindSubsystem<SubsystemWorldLifecycle>(true)!.Persistence.Save(false);
                }

                _httpQueue.Update();
                ExecuteConsoleCommands();
                ExecuteConsoleSuggestions();
                Dispatcher.AfterFrame();
                Time.AfterFrame();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            TickStatistics.Record(Stopwatch.GetElapsedTime(tickStarted).TotalMilliseconds, startLateness);
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
        if (!_interactiveConsole)
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
                        _consoleCommands.Enqueue(
                            new ConsoleCommandRequest(
                                line,
                                WriteResultToLog: true,
                                Completion: null
                            )
                        );
                    }
                }
            }
        );
    }

    private static void ExecuteConsoleCommands()
    {
        while (_consoleCommands.TryDequeue(out var request))
        {
            var result = CommandExecutor.ExecuteLocal(
                _modRuntime?.Commands,
                request.Input,
                _worlds.Runtime?.Authority?.Project
            );
            if (_worlds.Runtime?.Authority?.Project is { } project)
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
        var output = $"COMMAND {level} [{result.Code}] {TextResolver.Resolve(result.Message)}";
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
            ServerCommandSuggestions result;
            try
            {
                if (CurrentModRuntime.Value is not { } runtime)
                {
                    result = EmptyConsoleSuggestions();
                }
                else
                {
                    var adapter = new TextCommandAdapter(runtime.Commands, _worlds.Runtime?.Authority?.Project);
                    var items = adapter.Suggest(
                            request.Input,
                            CommandPrincipal.LocalOperator,
                            CommandInvocationChannel.Console
                        )
                        .OrderBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                        .Select(item => item with
                            {
                                Description = item.DescriptionSource is { } source
                                    ? TextResolver.Resolve(source)
                                    : item.Description
                            }
                        )
                        .ToArray();
                    result = new ServerCommandSuggestions(
                        items,
                        adapter.CanExecute(
                            request.Input,
                            CommandPrincipal.LocalOperator
                        )
                    );
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

    private static ServerCommandSuggestions EmptyConsoleSuggestions()
    {
        return new ServerCommandSuggestions([], false);
    }

    private static CommandResult CreateCommandConsoleUnavailableResult()
    {
        return CommandResult.LocalizedFail(
            "command.unavailable",
            "CommandUnavailable_Message",
            "指令系统尚未就绪。"
        );
    }

    private static void WriteAdministrationBootstrapInstructions()
    {
        if (_worlds.Runtime?.Authority?.Project is not { } project ||
            !ServerAdministrationBootstrap.TryGetClaimCode(project, out var code))
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(
            ResolveText(
                "AuthConsoleBootstrap_Message",
                "服务器管理尚未初始化\n当前没有玩家拥有管理权限。\n认领码：{0}\n初始化管理权限：\n1. 以玩家身份连接此服务器。\n2. 执行：/auth claim {0}\n如有需要，可在此控制台执行 auth code 或 auth regenerate。",
                code
            )
        );
        Console.WriteLine();
    }

    private static string ResolveText(string key, string fallback, params object[] args)
    {
        var format = TextResolver.Resolve(new LocalizedText("Commands", key, fallback));
        return args.Length == 0 ? format : string.Format(CultureInfo.InvariantCulture, format, args);
    }

    private static bool InitializeServices(StartupContext startup)
    {
        _preferences = new PreferenceStore(GamePaths.SettingsFile, [HostingConfiguration.Preferences]);
        _preferences.Load();
        if (HostingConfiguration.Preferences.EnsureHttpCommandAccessToken()) { _preferences.Save(); }

        LocalIdentityManager.Initialize(Storage.GetSystemPath(GamePaths.LocalIdentitiesFile));
        ContentRepositoryManager.Initialize();
        RendezvousManager.Initialize();
        ServerDirectoryManager.Initialize();
        _repository = new WorldRepository(GamePaths.Worlds, [], _activity, WorldProjectPersistence.WaitForPendingSaves);
        ModProfileManager.Initialize(_repository);
        var profile = ModProfileManager.LoadEffectiveProfile(startup.Session.SessionId, startup.Session);
        ModProfileResolver.EnsurePackagesAvailable(
            profile,
            Storage.GetSystemPath(GamePaths.ContentPackageCache),
            Log.Information
        );
        _modRuntime = GameModRuntime.StartFromProfile(
            profile,
            Storage.GetSystemPath(GamePaths.ContentPackageCache),
            Log.Information,
            registerAppCommands: _registerAppCommands ?? RegisterProcessCommands
        );
        using (var replacement = CurrentModRuntime.BeginReplacement(_modRuntime)) { replacement.Commit(); }

        _modRuntime.InitializeContentData();
        LanguageCatalog =
            _modRuntime.Content.CreateLanguageCatalog(HostingConfiguration.Preferences.ConsoleLanguageType);
        Volatile.Write(ref _resolver, new LocalizedTextResolver(LanguageCatalog));
        var options = new NetNodeOptions(
            Math.Max(1, HostingConfiguration.Preferences.ServerChunkCountSendPer),
            Math.Max(1, HostingConfiguration.Preferences.ServerChunkCountSendPer),
            Math.Max(1, HostingConfiguration.Preferences.ServerChunkBytesSendPerSecond)
        );
        var builder = new PackageHandlerCatalogBuilder();
        HostRuleHandlers.RegisterPreWorld(builder);
        _net = new NetNode(options, false, builder.Build(), NodeRole.Inactive);
        return true;
    }

    private static void RegisterProcessCommands(IModCommands commands, ModId owner)
    {
        new BuiltInProcessCommands(ProcessCommands, CommandDiagnostics)
            .Register(commands, owner);
    }

    private static bool StartListener(SessionInfo session, WorldInfo world)
    {
        return Game.Servers.ServerListener.Start(
            _net!,
            HostingConfiguration.Preferences,
            LocalIdentityManager.Current,
            session,
            world.WorldSettings,
            world.Hosting
        );
    }

    private static void StartHttp(SessionInfo session)
    {
        var preferences = HostingConfiguration.Preferences;
        if (!(session.HttpCommandEnabled ?? preferences.HttpCommandEnabled)) { return; }

        var port = session.HttpCommandPort ?? preferences.HttpCommandPort;
        var token = string.IsNullOrWhiteSpace(session.HttpCommandAccessToken)
            ? preferences.HttpCommandAccessToken
            : session.HttpCommandAccessToken;
        try
        {
            _http = HttpCommandHost.Start(
                port,
                token,
                CommandPrincipal.LocalOperator,
                _httpQueue,
                null,
                () => TextResolver
            );
        }
        catch (Exception exception)
        {
            Log.Error(
                $"HTTP command host could not listen on 127.0.0.1:{port}; the game will continue without HTTP command access. {exception.Message}"
            );
        }
    }
}
