using System.Collections.Concurrent;

using EntitySystem.Core;

using Game.Application;
using Game.Content;
using Game.FileStorage;
using Game.Hosting;
using Game.Modding.Blocks;
using Game.Network;
using Game.Network.Transport;

namespace Game.Managers;

public static class GameManager
{
    private static bool _leavingGameplay;

    private static readonly ConcurrentDictionary<Guid, (long Generation, bool ShowDialog)> _saveNotifications = new();

    public static Project? ClientProject => Runtime?.Client?.Project;

    private static long _runtimeGeneration;

    internal static WorldProjectActivity WorldActivity { get; } = new();

    private static readonly GameWorldController _worlds = new(WorldActivity, Dispatcher.InvokeAsync);

    public static GameRuntime? Runtime => _worlds.Runtime;

    public static ComponentCharacter? LocalCharacter =>
        Runtime?.Client?.Project?.FindSubsystem<SubsystemCharacters>()?.LocalCharacter;

    public static WorldInfo? WorldInfo => _worlds.WorldInfo;

    /// <summary>The startup session selecting this world, independent of runtime world ownership.</summary>
    public static SessionInfo? WorldSession => _worlds.WorldSession;

    public static LocalGameReturnTarget? LocalPlayerReturnTarget
    {
        get => _worlds.LocalPlayerReturnTarget;
        internal set => _worlds.LocalPlayerReturnTarget = value;
    }

    static GameManager()
    {
        _worlds.AuthorityLoaded += BindWorldLifecycle;
    }

    private static void OnNetworkStopped(
        NetNode node,
        WorldRuntimeContext stoppedRuntime,
        NodeRole previousRole,
        NodeStopReason reason
    )
    {
        if (Runtime is { IsDisposed: true } or { IsChangingWorlds: true }) { return; }

        if (Runtime is { } gameRuntime)
        {
            if (!ReferenceEquals(gameRuntime.Authority, stoppedRuntime) &&
                !ReferenceEquals(gameRuntime.Client, stoppedRuntime)) { return; }
        }
        else if (!ReferenceEquals(node, CommonLib.Net)) { return; }

        if (!Dispatcher.IsMainThread)
        {
            Dispatcher.Dispatch(() => OnNetworkStopped(node, stoppedRuntime, previousRole, reason));
            return;
        }

        if (Window.IsCreated)
        {
            LeaveGameplay(previousRole, Network.Presentation.DisconnectReasonText.Format(reason));
        }
    }

    public static Task LoadProjectAsync(
        WorldInfo worldInfo,
        SessionInfo? startupSession = null,
        CancellationToken cancellation = default
    )
    {
        var session = startupSession ?? SessionInfoManager.CreateWorldSession(worldInfo);
        var settings = Configuration.HostingConfiguration.Preferences;
        var mods = CurrentModRuntime.Value!;
        Network.Handling.ApplicationMessageHandlers.InstallWorldHandlers(CommonLib.Net, mods);
        _runtimeGeneration++;
        return _worlds.LoadAuthorityAsync(
            new WorldLoadRequest(
                worldInfo,
                session,
                CommonLib.Net,
                mods,
                DatabaseManager.GameDatabase,
                WorldProjectPolicies.CaptureAuthority(settings),
                new BlockTextureStore(GamePaths.BlockTextures),
                session.Target == SessionTarget.World
            ),
            cancellation
        );
    }

    public static void LoadClientProject(
        WorldRuntimeContext runtime,
        byte[] messagePackData,
        ContainerWidget gamesWidget,
        BlockIndexMapping mapping
    )
    {
        if (runtime.IsAuthority || runtime.IsClosed || runtime.HasProjectResources)
        {
            throw new InvalidOperationException("A bootstrap requires an unused client runtime.");
        }

        _worlds.AttachClient(runtime);
        var gameRuntime = _worlds.RequireRuntime();
        if (!ReferenceEquals(gameRuntime.Client, runtime))
        {
            throw new InvalidOperationException("Release the previous client before applying bootstrap.");
        }

        try
        {
            gameRuntime.AttachClient(runtime);
            var project = WorldProjectFactory.CreateClient(
                runtime,
                CurrentModRuntime.Value!,
                DatabaseManager.GameDatabase,
                messagePackData,
                gamesWidget,
                mapping,
                runtime.HasPresentation ? ClientApplication.Content : null,
                WorldInfo,
                Runtime?.Authority,
                (WorldSession ?? StartupManager.Current.Session).AutoJoinCharacterName,
                gameRuntime.IsMultiplayer
            );
            if (ClientApplication.World is { } presentation)
            {
                presentation.SetMultiplayer(gameRuntime.IsMultiplayer);
                presentation.SaveRequested +=
                    request => ObserveApplicationTask(SaveProjectAsync(request.ShowErrorDialog));
                presentation.LeaveRequested += () => LeaveGameplay();
                project.FindSubsystem<SubsystemCharacters>(true)!.CharacterCreationRejected += error =>
                    ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!
                        .ShowCharacterCreationFailure(presentation.Runtime, error);
                presentation.EnterRequested += () => ScreensManager.FindScreen<GameLoadingScreen>("GameLoading", true)!
                    .ContinueCharacterPreparation(presentation.Runtime);
            }

            SetupNetworkHandlers(project);
            WorldActivity.Track(runtime, null, Dispatcher.InvokeAsync);

            Log.Information("加载NetProject");
        }
        catch
        {
            ObserveApplicationTask(gameRuntime.ReleaseClientAsync());
            throw;
        }
    }

    public static async Task SaveProjectAsync(bool showErrorDialog)
    {
        var execution = Runtime?.AuthorityExecution;
        if (execution == null) { return; }

        var id = Guid.NewGuid();
        var worldName = WorldInfo?.WorldSettings.Name ?? string.Empty;
        _saveNotifications[id] = (_runtimeGeneration, showErrorDialog);
        try
        {
            var pending = await execution.InvokeAsync(world =>
                WorldProjectPersistence.For(world.Project!).SaveAsync(id)
            ).ConfigureAwait(false);
            var outcome = await pending.ConfigureAwait(false);
            if (outcome.Failure != null) { throw new WorldSaveException(outcome.WorldName, outcome.Failure); }
        }
        catch (Exception error)
        {
            if (_saveNotifications.TryRemove(id, out var notification))
            {
                ReportSaveFailure(worldName, notification.Generation, notification.ShowDialog, error);
            }

            throw;
        }
    }

    internal static void ObserveApplicationTask(Task operation)
    {
        _ = operation.ContinueWith(
            completed =>
            {
                if (completed.IsFaulted) { Log.Error(completed.Exception); }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default
        );
    }

    private static Task? _leaveOperation;

    public static Task PendingWorldTransition => _leaveOperation ?? Task.CompletedTask;

    private static void BindWorldLifecycle(Project project)
    {
        var generation = _runtimeGeneration;
        var lifecycle = project.FindSubsystem<SubsystemWorldLifecycle>(true)!;
        lifecycle.Persistence.SaveCompleted += outcome =>
        {
            var notification = _saveNotifications.TryRemove(outcome.OperationId, out var requested)
                ? requested
                : (Generation: generation, ShowDialog: true);
            if (outcome.Failure is not null)
            {
                ReportSaveFailure(outcome.WorldName, notification.Generation, notification.ShowDialog, outcome.Failure);
            }
        };
        lifecycle.Stopped += stopped =>
        {
            if (!ReferenceEquals(Runtime?.Authority, stopped.World)) { return; }

            Dispatcher.Dispatch(() =>
                {
                    if (ReferenceEquals(Runtime?.Authority, stopped.World) && Runtime is { IsChangingWorlds: false })
                    {
                        LeaveGameplay(NodeRole.Authority, null);
                    }
                }
            );
        };
    }

    private static void ReportSaveFailure(string worldName, long generation, bool showDialog, Exception exception)
    {
        if (!showDialog || !Window.IsCreated) { return; }

        Dispatcher.Dispatch(() =>
            {
                if (Window.IsCreated && _runtimeGeneration == generation)
                {
                    DialogsManager.ShowDialog(
                        null,
                        new MessageDialog(
                            string.Format(
                                Localization.ClientLanguage.Catalog.Get("GameManager", "SaveFailedTitle"),
                                worldName
                            ),
                            string.Format(
                                Localization.ClientLanguage.Catalog.Get("GameManager", "SaveFailedMessage"),
                                exception.Message
                            ),
                            Localization.ClientLanguage.Catalog.Ok
                        )
                    );
                }
            }
        );
    }

    public static void UpdateRuntime()
    {
        if (Runtime is { } gameRuntime)
        {
            var local = gameRuntime.Client?.Project?.FindSubsystem<SubsystemCharacters>()?.LocalCharacter;
            var gui = local?.Entity.FindComponent<ComponentCharacterPresentation>(true)?.GuiWidget;
            var menu = gui != null &&
                       DialogsManager.ReadOnlyDialogs.Any(dialog =>
                           dialog is GameMenuDialog && ReferenceEquals(dialog.ParentWidget, gui)
                       );
            gameRuntime.SetLocalPause(menu);
            gameRuntime.UpdateClient(Time.FrameDuration, Time.FrameStartTime);
        }
        else { CommonLib.Net.Update(); }
    }

    /// <summary>Ends GUI gameplay and returns according to the current player's runtime entry flow.</summary>
    public static void LeaveGameplay(string? reason = null) => LeaveGameplay(CommonLib.Net.Role, reason);

    private static void LeaveGameplay(NodeRole previousRole, string? reason)
    {
        if (_leavingGameplay) { return; }

        _leavingGameplay = true;
        _leaveOperation = LeaveGameplayCoreAsync(previousRole, reason);
        ObserveApplicationTask(_leaveOperation);
    }

    private static async Task LeaveGameplayCoreAsync(NodeRole previousRole, string? reason)
    {
        var hosted = LocalPlayerReturnTarget == LocalGameReturnTarget.HostedWorld &&
                     Runtime?.AuthorityExecution?.Status.State == AuthorityExecutionState.Running &&
                     Runtime.Authority is { IsClosed: false };
        var worldList = LocalPlayerReturnTarget == LocalGameReturnTarget.WorldList;
        try
        {
            if (hosted) { await LeaveLocalPlayerAsync().ConfigureAwait(false); }
            else { await CloseWorldAsync().ConfigureAwait(false); }

            await Dispatcher.InvokeAsync(() =>
                {
                    ScreensManager.SwitchScreen(
                        hosted ? "HostedWorld" : !worldList && previousRole == NodeRole.Client ? "NetPlay" : "Play"
                    );
                    if (!string.IsNullOrEmpty(reason)) { DialogsManager.Alert(reason); }
                }
            ).ConfigureAwait(false);
        }
        finally { await Dispatcher.InvokeAsync(() => _leavingGameplay = false).ConfigureAwait(false); }
    }

    public static async Task LeaveLocalPlayerAsync()
    {
        var runtime = Runtime;
        var presentation = ClientApplication.World;
        await SaveProjectAsync(true).ConfigureAwait(false);
        if (runtime?.AuthorityExecution is { } execution)
        {
            await execution.InvokeAsync(world =>
                {
                    WorldNetworkBinding.For(world).RequireNode().EndAdmission();
                    return true;
                }
            ).ConfigureAwait(false);
        }

        if (runtime != null) { await runtime.ReleaseClientAsync().ConfigureAwait(false); }

        await Dispatcher.InvokeAsync(() =>
            {
                if (presentation != null) { ClientApplication.ReleaseWorld(presentation); }

                if (ReferenceEquals(Runtime, runtime)) { LocalPlayerReturnTarget = null; }
            }
        ).ConfigureAwait(false);
    }

    public static async Task CloseWorldAsync()
    {
        var presentation = ClientApplication.World;
        await _worlds.CloseAsync(true).ConfigureAwait(false);
        if (presentation != null)
        {
            await Dispatcher.InvokeAsync(() => ClientApplication.ReleaseWorld(presentation)).ConfigureAwait(false);
        }
    }

    internal static void WaitForPendingSave() => WorldProjectPersistence.WaitForPendingSaves();

    public static async Task DisposeProjectAsync()
    {
        var presentation = ClientApplication.World;
        await _worlds.CloseAsync().ConfigureAwait(false);
        if (presentation != null)
        {
            await Dispatcher.InvokeAsync(() => ClientApplication.ReleaseWorld(presentation)).ConfigureAwait(false);
        }
    }

    internal static void FinalizePlatformShutdown()
    {
        // A destroyed platform view cannot pump frame continuations. Normal window close already awaited CloseWorldAsync.
        var runtime = Runtime;
        runtime?.ReleaseClientForHostShutdown();
        ClientApplication.ReleaseWorld();
        if (runtime?.AuthorityExecution is { } execution)
        {
            try { execution.StopAsync(true).GetAwaiter().GetResult(); }
            catch (Exception error) { Log.Error($"Final platform save failed: {error}"); }
        }

        _worlds.CloseAsync().GetAwaiter().GetResult();
    }

    private static void SetupNetworkHandlers(Project project)
    {
        var runtime = WorldRuntimeContext.For(project);
        var netNode = WorldNetworkBinding.For(runtime).RequireNode();

        netNode.TurnOnPackageHandle(project);

        if (runtime.IsAuthority && netNode.IsServer)
        {
            AuthorityWorldNetworkSetup.Attach(project, netNode, new BlockTextureStore(GamePaths.BlockTextures));
            runtime.OwnWorldResource(new WorldTimeReplication(project));
        }

        if (!runtime.IsAuthority)
        {
            SetupClientNetworkHandlers(project, netNode);
        }
    }

    internal static void ObserveEndpoint(NetNode node)
    {
        node.Stopped -= OnNetworkStopped;
        node.Stopped += OnNetworkStopped;
    }

    private static void SetupClientNetworkHandlers(Project project, NetNode net)
    {
        net.Connections.Authority!.ConnectionPhase = ConnectionPhase.BootstrapApplied;
        net.QueuePackage(
            new ConnectionPhaseAckPackage(
                net.Connections.Authority!.ConnectionEpoch,
                ConnectionPhase.BootstrapApplied
            ),
            PackageAudience.To(net.Server!)
        );
        net.OnClientStateChanged += c =>
        {
            if (c.State != ClientState.NotConnected)
            {
                return;
            }

            var subsystemCharacters = project.FindSubsystem<SubsystemCharacters>(true)!;
            var characterData = subsystemCharacters.Characters.Find(pd => pd.CharacterId == c.CharacterId);
            if (characterData is { IsLocalCharacter: false })
            {
                subsystemCharacters.MakeCharacterOffline(characterData.CharacterId);
            }
        };
    }
}
